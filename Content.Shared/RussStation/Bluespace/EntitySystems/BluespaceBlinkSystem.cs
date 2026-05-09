// HONK - Issue #302: see BluespaceCrushTeleportComponent for the design rationale.

using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.RussStation.Bluespace.Components;
using Content.Shared.Stacks;
using Content.Shared.Throwing;
using System;
using System.Numerics;
using Content.Shared.Random.Helpers;
using Content.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.RussStation.Bluespace.EntitySystems;

public sealed class BluespaceBlinkSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ThrownItemSystem _thrown = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    private const int BlinkCandidateAttempts = 12;

    private static readonly SoundSpecifier DepartureSound =
        new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");

    private static readonly SoundSpecifier ArrivalSound =
        new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceCrushTeleportComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<BluespaceCrushTeleportComponent, ThrowDoHitEvent>(OnThrowDoHit);
    }

    private void OnUseInHand(Entity<BluespaceCrushTeleportComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        // UseDelayComponent gates spam-pressing the use key. Bail before we
        // consume the crystal so a press during cooldown doesn't burn a charge.
        if (TryComp(ent.Owner, out UseDelayComponent? useDelay)
            && !_useDelay.TryResetDelay((ent.Owner, useDelay), checkDelayed: true))
            return;

        if (!TryConsumeOne(ent.Owner))
            return;

        _popup.PopupPredicted(
            Loc.GetString("bluespace-crystal-crush-self"),
            Loc.GetString("bluespace-crystal-crush-others", ("user", args.User)),
            args.User,
            args.User);

        Blink(args.User, ent.Comp);
        args.Handled = true;
    }

    private void OnThrowDoHit(Entity<BluespaceCrushTeleportComponent> ent, ref ThrowDoHitEvent args)
    {
        // Only blink when the impact lands on a mob (not a wall or item). Catching the
        // crystal mid-air doesn't trigger the effect; that's handled by the catch system
        // before this event fires.
        if (!HasComp<MobStateComponent>(args.Target))
            return;

        // Stop the crystal at the mob it hit. StopThrow alone strips the throwing
        // fixture and the ThrownItemComponent but leaves the existing linear
        // velocity intact, so the crystal keeps coasting past the target until
        // friction damps it. Zero the velocity first so it actually parks on
        // impact.
        if (TryComp<PhysicsComponent>(ent.Owner, out var thrownPhysics))
            _physics.SetLinearVelocity(ent.Owner, Vector2.Zero, body: thrownPhysics);
        _thrown.StopThrow(ent.Owner, args.Component);

        // Throw collisions are physics-driven and don't reconcile cleanly between
        // client and server: long-range arcs in particular can have the client predict
        // a collision the server never sees (effect plays, no teleport) or vice
        // versa (flicker to predicted spot, then snap back). Run the consume + blink
        // server-only so the teleport is authoritative. The client picks up the new
        // position on the next state update; visual effects spawned inside Blink use
        // PredictedSpawnAtPosition so they still appear without the hop.
        if (_net.IsClient)
            return;

        if (!TryConsumeOne(ent.Owner))
            return;

        Blink(args.Target, ent.Comp);
    }

    private bool TryConsumeOne(EntityUid uid)
    {
        // Predicted on both client and server, but only the server owns the stack count
        // and entity removal so that's where TryUse needs to land.
        if (_net.IsClient)
            return TryComp<StackComponent>(uid, out var stack) && stack.Count > 0;

        if (!TryComp<StackComponent>(uid, out var serverStack))
        {
            QueueDel(uid);
            return true;
        }

        if (!_stack.TryUse((uid, serverStack), 1))
            return false;

        if (serverStack.Count <= 0)
            QueueDel(uid);

        return true;
    }

    private void Blink(EntityUid target, BluespaceCrushTeleportComponent comp)
    {
        TryBlink(target, comp.BlinkRange, comp.BlinkEffectSource, comp.BlinkEffectDestination);
    }

    /// <summary>
    /// Public blink entry point so non-component callers (reagent metabolism, gas
    /// reactions, future fork mechanics) can fire the same teleport that the
    /// crystal's UseInHand and ThrowDoHit use, with matching VFX, audio,
    /// tile-blocked filter, and predicted RNG.
    /// </summary>
    public bool TryBlink(
        EntityUid target,
        float range,
        EntProtoId? sourceEffect = null,
        EntProtoId? destEffect = null)
    {
        var sourceXform = Transform(target);
        var coords = sourceXform.Coordinates;

        // Use the canonical predicted RNG helper (deterministic djb2 over CurTick
        // + NetEntity id) so the client's predicted blink lands on the exact
        // same tile the server picks. HashCode.Combine + EntityUid.Id would
        // diverge: HashCode.Combine uses a per-process random salt, and
        // EntityUid.Id is local rather than network-stable.
        var rng = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(target));

        if (!TryFindBlinkDestination(rng, coords, range, out var dest))
            return false;

        // Spawn the departure VFX + sound at the source tile (matches SS13's
        // attack_self / throw_impact sparks + SFX_PORTAL_ENTER).
        if (sourceEffect is { } src)
            PredictedSpawnAtPosition(src, coords);
        _audio.PlayPredicted(DepartureSound, coords, target);

        _xform.AttachToGridOrMap(target);
        _joints.ClearJoints(target);
        _xform.SetCoordinates(target, sourceXform, dest);

        // And again at the destination (matches do_teleport's phasein.ogg
        // arrival cue).
        if (destEffect is { } dst)
            PredictedSpawnAtPosition(dst, dest);
        _audio.PlayPredicted(ArrivalSound, target, target);
        return true;
    }

    private bool TryFindBlinkDestination(System.Random rng, EntityCoordinates source, float range, out EntityCoordinates dest)
    {
        // Try several candidate offsets and pick the first one that lands on a
        // walkable tile. Without this filter the target can end up wedged
        // between a wall and a dense entity (e.g. a machine) and get stuck.
        // If every candidate is blocked the crystal still gets consumed, but
        // the target stays put; that mirrors SS13's "the bluespace fizzled"
        // behaviour in tight spaces.
        for (var i = 0; i < BlinkCandidateAttempts; i++)
        {
            var angle = rng.NextDouble() * Math.PI * 2.0;
            var magnitude = (float)(rng.NextDouble() * range);
            var offset = new System.Numerics.Vector2(
                (float)Math.Cos(angle) * magnitude,
                (float)Math.Sin(angle) * magnitude);
            var candidate = source.Offset(offset);

            if (!_turf.TryGetTileRef(candidate, out var tile))
                continue;

            if (_turf.IsSpace(tile.Value))
                continue;

            if (_turf.IsTileBlocked(tile.Value, CollisionGroup.MobMask))
                continue;

            dest = _turf.GetTileCenter(tile.Value);
            return true;
        }

        dest = default;
        return false;
    }
}

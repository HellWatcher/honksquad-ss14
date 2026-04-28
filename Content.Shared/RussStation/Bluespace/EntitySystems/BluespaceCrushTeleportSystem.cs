// HONK - Issue #302: see BluespaceCrushTeleportComponent for the design rationale.

using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Bluespace.Components;
using Content.Shared.Stacks;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Shared.RussStation.Bluespace.EntitySystems;

public sealed class BluespaceCrushTeleportSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private static readonly SoundSpecifier PortalSound =
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

        if (!TryConsumeOne(ent.Owner))
            return;

        _popup.PopupPredicted(
            Loc.GetString("bluespace-crystal-crush-self"),
            Loc.GetString("bluespace-crystal-crush-others", ("user", args.User)),
            args.User,
            args.User);

        Blink(args.User, ent.Comp.BlinkRange);
        args.Handled = true;
    }

    private void OnThrowDoHit(Entity<BluespaceCrushTeleportComponent> ent, ref ThrowDoHitEvent args)
    {
        // Only blink when the impact lands on a mob (not a wall or item). Catching the
        // crystal mid-air doesn't trigger the effect; that's handled by the catch system
        // before this event fires.
        if (!HasComp<MobStateComponent>(args.Target))
            return;

        if (!TryConsumeOne(ent.Owner))
            return;

        Blink(args.Target, ent.Comp.BlinkRange);
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

    private void Blink(EntityUid target, float range)
    {
        var sourceXform = Transform(target);
        var coords = sourceXform.Coordinates;
        var offset = _random.NextVector2(range);
        var dest = coords.Offset(offset);

        _xform.AttachToGridOrMap(target);
        _joints.ClearJoints(target);
        _xform.SetCoordinates(target, sourceXform, dest);

        _audio.PlayPredicted(PortalSound, target, target);
    }
}

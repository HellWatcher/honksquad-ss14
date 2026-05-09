// HONK - Issue #697 parent: shared bluespace plant teleport + backfire mechanics.
// See BluespacePlantTeleportComponent and BluespacePlantBackfireComponent for the
// per-component design. The actual blink (range + tile filter + VFX + audio +
// predicted RNG) is delegated to BluespaceBlinkSystem.TryBlink so plants behave
// identically to crushing a crystal in hand or drinking bluespace dust.

using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Bluespace.Components;
using Content.Shared.Slippery;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.RussStation.Bluespace.EntitySystems;

public sealed class BluespacePlantTeleportSystem : EntitySystem
{
    [Dependency] private readonly BluespaceBlinkSystem _blink = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    // Plant teleports always reuse the canonical bluespace VFX so they read the
    // same as a crystal crush. Pass these explicitly to TryBlink (its defaults
    // are null so callers can opt out of VFX entirely).
    private const string SourceEffect = "EffectPhaseOut";
    private const string DestEffect = "EffectPhaseIn";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespacePlantTeleportComponent, ThrowDoHitEvent>(OnThrowHit);
        SubscribeLocalEvent<BluespacePlantTeleportComponent, SlipEvent>(OnSlip);
        SubscribeLocalEvent<BluespacePlantBackfireComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnThrowHit(Entity<BluespacePlantTeleportComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!ent.Comp.OnThrowImpact)
            return;

        // Only blink living mobs; throwing the fruit at a wall or item shouldn't fire.
        if (!HasComp<MobStateComponent>(args.Target))
            return;

        _blink.TryBlink(args.Target, ent.Comp.BlinkRange, SourceEffect, DestEffect);
    }

    private void OnSlip(Entity<BluespacePlantTeleportComponent> ent, ref SlipEvent args)
    {
        if (!ent.Comp.OnSlip)
            return;

        _blink.TryBlink(args.Slipped, ent.Comp.BlinkRange, SourceEffect, DestEffect);
    }

    private void OnUseInHand(Entity<BluespacePlantBackfireComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!_random.Prob(ent.Comp.Probability))
            return;

        // Fumble: pop the message client-side via predicted popup, blink the user, and
        // delete the fruit (mimics SS13's squash_plant on the holder).
        _popup.PopupPredicted(
            Loc.GetString("bluespace-plant-backfire-self"),
            Loc.GetString("bluespace-plant-backfire-others", ("user", args.User)),
            args.User,
            args.User);

        _blink.TryBlink(args.User, ent.Comp.BlinkRange, SourceEffect, DestEffect);

        // Server owns entity destruction; client just marks the use as handled.
        if (_net.IsServer)
            QueueDel(ent.Owner);

        args.Handled = true;
    }
}

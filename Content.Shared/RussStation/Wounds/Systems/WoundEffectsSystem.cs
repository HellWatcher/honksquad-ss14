using Content.Shared.Alert;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.HealthExaminable;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.RussStation.Wounds.Systems;

/// <summary>
/// Applies gameplay effects based on active wounds:
/// movement slow, and item drop on hit for tier 3 fractures.
/// </summary>
public sealed class WoundEffectsSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedWoundSystem _wounds = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Movement speed multiplier applied for tier 2+ fractures and tier 2+ burns.
    /// </summary>
    private const float MovementSlowMultiplier = 0.7f;

    /// <summary>
    /// Chance to drop a held item when hit with a tier 3 fracture.
    /// </summary>
    private const float DropChance = 0.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<WoundComponent, WoundsDamagedEvent>(OnWoundsDamaged);
        SubscribeLocalEvent<WoundComponent, WoundsClearedEvent>(OnWoundsCleared);
        SubscribeLocalEvent<WoundComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WoundComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WoundComponent, HealthBeingExaminedEvent>(OnHealthExamined);
    }

    private void OnStartup(EntityUid uid, WoundComponent comp, ComponentStartup args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        RefreshAlerts(uid, comp);
    }

    private void OnRefreshSpeed(EntityUid uid, WoundComponent comp, ref RefreshMovementSpeedModifiersEvent args)
    {
        var fractureTier = _wounds.GetWorstTier(comp, WoundCategory.Fracture);
        var burnTier = _wounds.GetWorstTier(comp, WoundCategory.Burn);

        // Fractures: tier 2+ slows movement
        // Burns: tier 2+ slows movement
        if (fractureTier >= 2 || burnTier >= 2)
            args.ModifySpeed(MovementSlowMultiplier);
    }

    private void OnHealthExamined(EntityUid uid, WoundComponent comp, ref HealthBeingExaminedEvent args)
    {
        // Bleeding is already surfaced on the health-examine path by SharedBloodstreamSystem,
        // so we only flavor-text fractures and burns here. Self-Aware viewers get a separate
        // clinical readout via SelfAwareSystem and don't go through this event.
        var fractureTier = _wounds.GetWorstTier(comp, WoundCategory.Fracture);
        if (fractureTier > 0)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(
                Loc.GetString($"wound-examine-fracture-{fractureTier}", ("target", uid)));
        }

        var burnTier = _wounds.GetWorstTier(comp, WoundCategory.Burn);
        if (burnTier > 0)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(
                Loc.GetString($"wound-examine-burn-{burnTier}", ("target", uid)));
        }
    }

    private void OnShutdown(EntityUid uid, WoundComponent comp, ComponentShutdown args)
    {
        _alerts.ClearAlert(uid, comp.FractureAlert);
        _alerts.ClearAlert(uid, comp.BurnAlert);
    }

    private void OnWoundsDamaged(EntityUid uid, WoundComponent comp, WoundsDamagedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        RefreshAlerts(uid, comp);

        // Tier 3 fracture: chance to drop held items on hit
        var fractureTier = _wounds.GetWorstTier(comp, WoundCategory.Fracture);
        if (fractureTier < 3)
            return;

        if (!_random.Prob(DropChance))
            return;

        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        _hands.TryDrop((uid, hands));
    }

    private void OnWoundsCleared(EntityUid uid, WoundComponent comp, WoundsClearedEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        RefreshAlerts(uid, comp);
    }

    /// <summary>
    /// Shows or clears fracture/burn HUD alerts based on current wound state.
    /// </summary>
    public void RefreshAlerts(EntityUid uid, WoundComponent comp)
    {
        var fractureTier = _wounds.GetWorstTier(comp, WoundCategory.Fracture);
        var burnTier = _wounds.GetWorstTier(comp, WoundCategory.Burn);

        if (fractureTier > 0)
            _alerts.ShowAlert(uid, comp.FractureAlert, (short) (fractureTier - 1));
        else
            _alerts.ClearAlert(uid, comp.FractureAlert);

        if (burnTier > 0)
            _alerts.ShowAlert(uid, comp.BurnAlert, (short) (burnTier - 1));
        else
            _alerts.ClearAlert(uid, comp.BurnAlert);
    }
}

using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
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
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedWoundSystem _wounds = default!;
    [Dependency] private readonly WoundDisplaySystem _display = default!;
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
        SubscribeLocalEvent<WoundComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WoundComponent, ExaminedEvent>(OnExamined);
    }

    private void OnStartup(EntityUid uid, WoundComponent comp, ComponentStartup args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
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

    private void OnExamined(EntityUid uid, WoundComponent comp, ExaminedEvent args)
    {
        var wounds = _display.GetWoundDisplayInfo(uid, comp);
        if (wounds.Count == 0)
            return;

        using (args.PushGroup(nameof(WoundEffectsSystem)))
        {
            args.PushMarkup(Loc.GetString("wound-examine-header"));

            foreach (var wound in wounds)
            {
                var name = Loc.GetString(wound.LocKey);
                args.PushMarkup($"  - {name}");
            }
        }
    }

    private void OnWoundsDamaged(EntityUid uid, WoundComponent comp, WoundsDamagedEvent args)
    {
        if (_timing.ApplyingState)
            return;

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
}

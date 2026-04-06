using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Flash;
using Content.Shared.Mobs;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.RussStation.Body;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Handles all passive effects for cybernetic organs installed in a body.
/// Each organ type has a marker component that enables its effect.
/// Subscribes to events on BodyComponent and scans organs for the relevant marker.
/// </summary>
public sealed class CyberneticOrganEffectsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Heart: auto-inject epinephrine on crit
        SubscribeLocalEvent<BodyComponent, MobStateChangedEvent>(OnMobStateChanged);

        // Eyes: flash protection
        SubscribeLocalEvent<BodyComponent, FlashAttemptEvent>(OnFlashAttempt);

        // Lungs: toxic gas filtering (must run before RespiratorSystem relays to lungs)
        SubscribeLocalEvent<BodyComponent, InhaledGasEvent>(OnInhaledGas,
            before: [typeof(RespiratorSystem)]);

        // Stomach: nutrient efficiency (reduced hunger decay)
        SubscribeLocalEvent<CyberneticStomachComponent, OrganGotInsertedEvent>(OnStomachInserted);
        SubscribeLocalEvent<CyberneticStomachComponent, OrganGotRemovedEvent>(OnStomachRemoved);
    }

    // ================================================================
    // Heart — epinephrine auto-injection on entering crit
    // ================================================================

    private void OnMobStateChanged(EntityUid uid, BodyComponent body, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical || body.Organs == null)
            return;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!TryComp<CyberneticHeartComponent>(organ, out var heart))
                continue;

            TryInjectEpinephrine(uid, organ, heart);
            break;
        }
    }

    private void TryInjectEpinephrine(EntityUid body, EntityUid organ, CyberneticHeartComponent heart)
    {
        var now = _timing.CurTime;

        if (heart.LastInjection != null && now - heart.LastInjection.Value < heart.Cooldown)
            return;

        var solution = new Solution(heart.Reagent, heart.InjectAmount);
        if (!_bloodstream.TryAddToBloodstream(body, solution))
            return;

        heart.LastInjection = now;
        Dirty(organ, heart);

        _popup.PopupEntity(
            Loc.GetString("cybernetic-heart-inject"),
            body, body, PopupType.MediumCaution);
    }

    // ================================================================
    // Eyes — flash protection
    // ================================================================

    private void OnFlashAttempt(EntityUid uid, BodyComponent body, ref FlashAttemptEvent args)
    {
        if (args.Cancelled || body.Organs == null)
            return;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (HasComp<CyberneticEyesComponent>(organ))
            {
                args.Cancelled = true;
                return;
            }
        }
    }

    // ================================================================
    // Lungs — toxic gas filtering
    // ================================================================

    /// <summary>
    /// Safe gases that cybernetic lungs should NOT filter.
    /// Everything else (plasma, tritium, ammonia, etc.) gets reduced.
    /// </summary>
    private static readonly Gas[] SafeGases = [Gas.Oxygen, Gas.Nitrogen, Gas.WaterVapor];

    private void OnInhaledGas(EntityUid uid, BodyComponent body, ref InhaledGasEvent args)
    {
        if (body.Organs == null)
            return;

        CyberneticLungsComponent? lungs = null;
        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (TryComp(organ, out lungs))
                break;
        }

        if (lungs == null)
            return;

        var gas = args.Gas;
        foreach (var gasId in Enum.GetValues<Gas>())
        {
            if (Array.IndexOf(SafeGases, gasId) >= 0)
                continue;

            var moles = gas[(int) gasId];
            if (moles <= 0f)
                continue;

            gas.SetMoles(gasId, moles * (1f - lungs.FilterFraction));
        }
    }

    // ================================================================
    // Stomach — nutrient efficiency (reduced hunger decay)
    // ================================================================

    private void OnStomachInserted(EntityUid uid, CyberneticStomachComponent stomach, ref OrganGotInsertedEvent args)
    {
        ApplyHungerDecayModifier(args.Target, stomach.DecayMultiplier);
    }

    private void OnStomachRemoved(EntityUid uid, CyberneticStomachComponent stomach, ref OrganGotRemovedEvent args)
    {
        ApplyHungerDecayModifier(args.Target, 1f / stomach.DecayMultiplier);
    }

    private void ApplyHungerDecayModifier(EntityUid body, float modifier)
    {
        if (!TryComp<HungerComponent>(body, out var hunger))
            return;

        _hunger.SetBaseDecayRate(body, hunger.BaseDecayRate * modifier, hunger);
    }
}

using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityConditions.Conditions.Body;
using Content.Shared.EntityEffects.Effects.Body;
using Content.Shared.Flash;
using Content.Shared.Metabolism;
using Content.Shared.Mobs;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.RussStation.Body;
using Robust.Shared.Prototypes;
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
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedAtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly Gas[] AllGases = Enum.GetValues<Gas>();

    public override void Initialize()
    {
        base.Initialize();

        // Heart: auto-inject epinephrine on crit
        SubscribeLocalEvent<BodyComponent, MobStateChangedEvent>(OnMobStateChanged);

        // Eyes: flash protection
        SubscribeLocalEvent<BodyComponent, FlashAttemptEvent>(OnFlashAttempt);

        // Lungs: toxic gas filtering (runs on the organ via relay, before RespiratorSystem processes it)
        SubscribeLocalEvent<CyberneticLungsComponent, OrganGotInsertedEvent>(OnLungsInserted);
        SubscribeLocalEvent<CyberneticLungsComponent, OrganGotRemovedEvent>(OnLungsRemoved);
        SubscribeLocalEvent<CyberneticLungsComponent, BodyRelayedEvent<InhaledGasEvent>>(OnInhaledGas,
            before: [typeof(RespiratorSystem)]);

        // Stomach: nutrient efficiency (reduced hunger decay)
        SubscribeLocalEvent<CyberneticStomachComponent, OrganGotInsertedEvent>(OnStomachInserted);
        SubscribeLocalEvent<CyberneticStomachComponent, OrganGotRemovedEvent>(OnStomachRemoved);

        // Ears: deafness resistance handled in shared DeafableSystem

        // Liver: overdose resistance (applied to body on insert/remove)
        SubscribeLocalEvent<CyberneticLiverComponent, OrganGotInsertedEvent>(OnLiverInserted);
        SubscribeLocalEvent<CyberneticLiverComponent, OrganGotRemovedEvent>(OnLiverRemoved);
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
        {
            Log.Warning($"Cybernetic heart failed to inject into {ToPrettyString(body)}");
            return;
        }

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

    private void OnLungsInserted(EntityUid uid, CyberneticLungsComponent lungs, ref OrganGotInsertedEvent args)
    {
        lungs.OxygenatingGases.Clear();

        // Find the host body's metabolizer types from any organ that has them.
        var metabolizerTypes = new HashSet<ProtoId<MetabolizerTypePrototype>>();
        if (TryComp<BodyComponent>(args.Target, out var body) && body.Organs != null)
        {
            foreach (var organ in body.Organs.ContainedEntities)
            {
                if (TryComp<MetabolizerComponent>(organ, out var met) && met.MetabolizerTypes != null)
                {
                    metabolizerTypes.UnionWith(met.MetabolizerTypes);
                }
            }
        }

        if (metabolizerTypes.Count == 0)
            return;

        // Scan gas reagent prototypes for Oxygenate effects matching the host's types.
        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var reagentId = _atmos.GasReagents[i];
            if (reagentId == null || !_proto.TryIndex<ReagentPrototype>(reagentId, out var reagent))
                continue;

            if (IsOxygenatingForTypes(reagent, metabolizerTypes))
                lungs.OxygenatingGases.Add((Gas) i);
        }
    }

    private void OnLungsRemoved(EntityUid uid, CyberneticLungsComponent lungs, ref OrganGotRemovedEvent args)
    {
        lungs.OxygenatingGases.Clear();
    }

    private bool IsOxygenatingForTypes(
        ReagentPrototype reagent,
        HashSet<ProtoId<MetabolizerTypePrototype>> types)
    {
        if (reagent.Metabolisms == null)
            return false;

        // Check both Respiration and Bloodstream stages (gases use both).
        foreach (var (_, entry) in reagent.Metabolisms.Metabolisms)
        {
            foreach (var effect in entry.Effects)
            {
                if (effect is not Oxygenate oxy || oxy.Factor <= 0f)
                    continue;

                if (effect.Conditions == null)
                    return true;

                foreach (var condition in effect.Conditions)
                {
                    if (condition is not MetabolizerTypeCondition metCond)
                        continue;

                    var matchesAny = metCond.Type.Any(t => types.Contains(t));
                    // Inverted means "NOT these types", so invert the result.
                    if (matchesAny != metCond.Inverted)
                        return true;
                }
            }
        }

        return false;
    }

    private void OnInhaledGas(Entity<CyberneticLungsComponent> ent, ref BodyRelayedEvent<InhaledGasEvent> args)
    {
        // If no oxygenating gases were resolved, skip filtering entirely
        // to avoid suffocating the patient by removing all gas.
        if (ent.Comp.OxygenatingGases.Count == 0)
            return;

        var gas = args.Args.Gas;

        foreach (var gasId in AllGases)
        {
            if (ent.Comp.OxygenatingGases.Contains(gasId))
                continue;

            var moles = gas[(int) gasId];
            if (moles <= 0f)
                continue;

            gas.SetMoles(gasId, moles * (1f - ent.Comp.FilterFraction));
        }
    }

    // ================================================================
    // Stomach — nutrient efficiency (reduced hunger decay)
    // ================================================================

    private void OnStomachInserted(EntityUid uid, CyberneticStomachComponent stomach, ref OrganGotInsertedEvent args)
    {
        if (!TryComp<HungerComponent>(args.Target, out var hunger))
            return;

        stomach.OriginalDecayRate = hunger.BaseDecayRate;
        _hunger.SetBaseDecayRate(args.Target, hunger.BaseDecayRate * stomach.DecayMultiplier, hunger);
    }

    private void OnStomachRemoved(EntityUid uid, CyberneticStomachComponent stomach, ref OrganGotRemovedEvent args)
    {
        if (stomach.OriginalDecayRate == null || !TryComp<HungerComponent>(args.Target, out var hunger))
            return;

        _hunger.SetBaseDecayRate(args.Target, stomach.OriginalDecayRate.Value, hunger);
        stomach.OriginalDecayRate = null;
    }

    // ================================================================
    // Liver — overdose resistance
    // ================================================================

    private void OnLiverInserted(EntityUid uid, CyberneticLiverComponent liver, ref OrganGotInsertedEvent args)
    {
        var resistance = EnsureComp<OverdoseResistanceComponent>(args.Target);
        resistance.ThresholdMultiplier = liver.OverdoseThresholdMultiplier;
        Dirty(args.Target, resistance);
    }

    private void OnLiverRemoved(EntityUid uid, CyberneticLiverComponent liver, ref OrganGotRemovedEvent args)
    {
        // Check if any other cybernetic liver remains in the body
        if (TryComp<BodyComponent>(args.Target, out var body) && body.Organs != null)
        {
            foreach (var organ in body.Organs.ContainedEntities)
            {
                if (organ != uid && HasComp<CyberneticLiverComponent>(organ))
                    return;
            }
        }

        RemComp<OverdoseResistanceComponent>(args.Target);
    }
}

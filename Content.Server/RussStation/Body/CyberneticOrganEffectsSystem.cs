using Content.Server.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Flash;
using Content.Shared.Mobs;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.RussStation.Body;
using Content.Shared.RussStation.Hearing;
using Robust.Shared.GameObjects;
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

        // Lungs: advanced lungs are a marker tier; toxic-gas filtering deferred to a follow-up PR.

        // Stomach: nutrient efficiency (reduced hunger decay)
        SubscribeLocalEvent<CyberneticStomachComponent, OrganGotInsertedEvent>(OnStomachInserted);
        SubscribeLocalEvent<CyberneticStomachComponent, OrganGotRemovedEvent>(OnStomachRemoved);

        // Basic ears: hearing impairment (muffled audio)
        SubscribeLocalEvent<CyberneticEarsBasicComponent, OrganGotInsertedEvent>(OnBasicEarsInserted);
        SubscribeLocalEvent<CyberneticEarsBasicComponent, OrganGotRemovedEvent>(OnBasicEarsRemoved);

        // Advanced eyes: flash immunity (handled via OnFlashAttempt scan).
        // The flashlight-style body light + toggle action live in SharedCyberneticEyeLightSystem.

        // Advanced ears: deafness resistance handled in shared DeafableSystem

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
    // Eyes — flash protection (Advanced tier)
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

    // ================================================================
    // Basic Ears — hearing impairment (muffled audio)
    // ================================================================

    private void OnBasicEarsInserted(EntityUid uid, CyberneticEarsBasicComponent comp, ref OrganGotInsertedEvent args)
    {
        EnsureComp<HearingImpairmentComponent>(args.Target);
    }

    private void OnBasicEarsRemoved(EntityUid uid, CyberneticEarsBasicComponent comp, ref OrganGotRemovedEvent args)
    {
        if (TryComp<BodyComponent>(args.Target, out var body) && body.Organs != null)
        {
            foreach (var organ in body.Organs.ContainedEntities)
            {
                if (organ != uid && HasComp<CyberneticEarsBasicComponent>(organ))
                    return;
            }
        }

        RemComp<HearingImpairmentComponent>(args.Target);
    }

}

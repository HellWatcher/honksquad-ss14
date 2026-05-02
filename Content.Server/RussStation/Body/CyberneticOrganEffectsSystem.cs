using Content.Server.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.RussStation.Body;
using Content.Shared.RussStation.Hearing;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Handles all passive effects for cybernetic organs installed in a body. Each organ type
/// has a marker component that enables its effect; per-organ PRs add more handlers in this
/// file as their PRs land.
/// </summary>
public sealed class CyberneticOrganEffectsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Heart: auto-inject epinephrine on crit
        SubscribeLocalEvent<BodyComponent, MobStateChangedEvent>(OnMobStateChanged);

        // Basic ears: hearing impairment (muffled audio)
        SubscribeLocalEvent<CyberneticEarsBasicComponent, OrganGotInsertedEvent>(OnBasicEarsInserted);
        SubscribeLocalEvent<CyberneticEarsBasicComponent, OrganGotRemovedEvent>(OnBasicEarsRemoved);
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
    // Basic ears — hearing impairment (muffled audio)
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

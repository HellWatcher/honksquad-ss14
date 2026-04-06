using Content.Server.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.RussStation.Body;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Auto-injects epinephrine into the host's bloodstream when they enter crit,
/// if they have an advanced cybernetic heart with <see cref="CyberneticHeartComponent"/>.
/// </summary>
public sealed class CyberneticHeartSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, BodyComponent body, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical)
            return;

        if (body.Organs == null)
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
}

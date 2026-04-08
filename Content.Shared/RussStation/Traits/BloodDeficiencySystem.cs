using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Disables natural blood regeneration and slowly drains blood over time.
/// Without treatment (transfusion, iron supplements, etc.) the entity will
/// eventually die from bloodloss.
/// </summary>
public sealed class BloodDeficiencySystem : EntitySystem
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodDeficiencyComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, BloodDeficiencyComponent component, MapInitEvent args)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

        bloodstream.BloodRefreshAmount = 0;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BloodDeficiencyComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var deficiency, out var bloodstream))
        {
            deficiency.Accumulator += frameTime;

            var interval = (float) bloodstream.AdjustedUpdateInterval.TotalSeconds;
            if (deficiency.Accumulator < interval)
                continue;

            deficiency.Accumulator -= interval;
            _bloodstream.TryModifyBloodLevel(uid, -deficiency.BloodLossPerTick);
        }
    }
}

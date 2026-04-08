using Content.Shared.Body.Components;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces blood regeneration rate for entities with BloodDeficiencyComponent
/// by modifying BloodRefreshAmount on MapInit.
/// </summary>
public sealed class BloodDeficiencySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodDeficiencyComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, BloodDeficiencyComponent component, MapInitEvent args)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

        bloodstream.BloodRefreshAmount *= component.BloodRefreshMultiplier;
    }
}

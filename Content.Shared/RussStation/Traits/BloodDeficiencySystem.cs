using Content.Shared.Body.Components;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Sets BloodRefreshAmount to a negative value on MapInit, causing
/// the bloodstream system to slowly drain blood each tick instead of
/// regenerating it.
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

        bloodstream.BloodRefreshAmount = -component.BloodLossPerTick;
    }
}

using Content.Shared.RussStation.Wounds;

namespace Content.Shared.RussStation.Traits;

public sealed class FrailSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FrailComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FrailComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(EntityUid uid, FrailComponent component, ComponentInit args)
    {
        if (!TryComp<WoundComponent>(uid, out var wound))
            return;

        wound.ThresholdMultiplier *= component.ThresholdMultiplier;
    }

    private void OnRemove(EntityUid uid, FrailComponent component, ComponentRemove args)
    {
        if (!TryComp<WoundComponent>(uid, out var wound))
            return;

        if (component.ThresholdMultiplier != 0f)
            wound.ThresholdMultiplier /= component.ThresholdMultiplier;
    }
}

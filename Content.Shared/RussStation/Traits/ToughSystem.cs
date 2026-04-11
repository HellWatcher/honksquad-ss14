using Content.Shared.RussStation.Wounds;

namespace Content.Shared.RussStation.Traits;

public sealed class ToughSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToughComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ToughComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(EntityUid uid, ToughComponent component, ComponentInit args)
    {
        if (!TryComp<WoundComponent>(uid, out var wound))
            return;

        wound.ThresholdMultiplier *= component.ThresholdMultiplier;
    }

    private void OnRemove(EntityUid uid, ToughComponent component, ComponentRemove args)
    {
        if (!TryComp<WoundComponent>(uid, out var wound))
            return;

        if (component.ThresholdMultiplier != 0f)
            wound.ThresholdMultiplier /= component.ThresholdMultiplier;
    }
}

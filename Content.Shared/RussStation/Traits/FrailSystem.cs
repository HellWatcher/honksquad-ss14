using Content.Shared.RussStation.Wounds;
using Content.Shared.RussStation.Wounds.Systems;

namespace Content.Shared.RussStation.Traits;

public sealed class FrailSystem : EntitySystem
{
    [Dependency] private readonly SharedWoundSystem _wound = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FrailComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FrailComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(EntityUid uid, FrailComponent component, ComponentInit args)
    {
        if (!HasComp<WoundComponent>(uid))
            return;

        _wound.ScaleThresholdMultiplier(uid, component.ThresholdMultiplier);
    }

    private void OnRemove(EntityUid uid, FrailComponent component, ComponentRemove args)
    {
        if (!HasComp<WoundComponent>(uid))
            return;

        if (component.ThresholdMultiplier != 0f)
            _wound.ScaleThresholdMultiplier(uid, 1f / component.ThresholdMultiplier);
    }
}

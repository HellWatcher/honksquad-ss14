using Content.Shared.RussStation.Wounds;
using Content.Shared.RussStation.Wounds.Systems;

namespace Content.Shared.RussStation.Traits;

public sealed class ToughSystem : EntitySystem
{
    [Dependency] private readonly SharedWoundSystem _wound = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToughComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ToughComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(EntityUid uid, ToughComponent component, ComponentInit args)
    {
        if (!HasComp<WoundComponent>(uid))
            return;

        _wound.ScaleThresholdMultiplier(uid, component.ThresholdMultiplier);
    }

    private void OnRemove(EntityUid uid, ToughComponent component, ComponentRemove args)
    {
        if (!HasComp<WoundComponent>(uid))
            return;

        if (component.ThresholdMultiplier != 0f)
            _wound.ScaleThresholdMultiplier(uid, 1f / component.ThresholdMultiplier);
    }
}

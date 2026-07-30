using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;

namespace Content.Shared.RussStation.Traits;

public sealed partial class IronJawSystem : EntitySystem
{
    [Dependency] private SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IronJawComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IronJawComponent, RefreshStaminaCritThresholdEvent>(OnRefreshCritThreshold);
    }

    private void OnMapInit(EntityUid uid, IronJawComponent component, MapInitEvent args)
    {
        if (!TryComp<StaminaComponent>(uid, out var stamina))
            return;

        _stamina.RefreshStaminaCritThreshold((uid, stamina));
    }

    private void OnRefreshCritThreshold(EntityUid uid, IronJawComponent component, ref RefreshStaminaCritThresholdEvent args)
    {
        args.Modifier *= component.CritThresholdModifier;
    }
}

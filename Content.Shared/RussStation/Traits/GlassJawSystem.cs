using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;

namespace Content.Shared.RussStation.Traits;

public sealed class GlassJawSystem : EntitySystem
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GlassJawComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GlassJawComponent, RefreshStaminaCritThresholdEvent>(OnRefreshCritThreshold);
    }

    private void OnMapInit(EntityUid uid, GlassJawComponent component, MapInitEvent args)
    {
        _stamina.RefreshStaminaCritThreshold((uid, null));
    }

    private void OnRefreshCritThreshold(EntityUid uid, GlassJawComponent component, ref RefreshStaminaCritThresholdEvent args)
    {
        args.Modifier *= component.CritThresholdModifier;
    }
}

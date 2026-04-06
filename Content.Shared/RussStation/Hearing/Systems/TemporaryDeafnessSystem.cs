using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Hearing.Systems;

public sealed class TemporaryDeafnessSystem : EntitySystem
{
    public static readonly ProtoId<StatusEffectPrototype> DeafnessStatusEffect = "TemporaryDeafness";

    [Dependency] private readonly DeafableSystem _deafable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemporaryDeafnessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TemporaryDeafnessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TemporaryDeafnessComponent, CanHearAttemptEvent>(OnTryHear);
    }

    private void OnStartup(EntityUid uid, TemporaryDeafnessComponent component, ComponentStartup args)
    {
        _deafable.UpdateIsDeaf(uid);
    }

    private void OnShutdown(EntityUid uid, TemporaryDeafnessComponent component, ComponentShutdown args)
    {
        _deafable.UpdateIsDeaf(uid);
    }

    private void OnTryHear(EntityUid uid, TemporaryDeafnessComponent component, CanHearAttemptEvent args)
    {
        if (component.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }
}

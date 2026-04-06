using Content.Server.Body.Systems;
using Content.Shared.Medical;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Handles the CardiacArrest status effect.
/// When active, stuns the target and rapidly drains respirator saturation
/// to simulate oxygen not reaching cells due to the heart not pumping blood.
/// Lungs still work, but can't keep up with the drain.
/// </summary>
public sealed class CardiacArrestSystem : EntitySystem
{
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    /// <summary>
    /// Extra saturation drained per second. Normal drain is ~1/s, so 3/s extra
    /// means lungs can't compensate and the entity suffocates.
    /// </summary>
    private const float DrainPerSecond = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardiacArrestComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<CardiacArrestComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<ActiveCardiacArrestComponent, TargetDefibrillatedEvent>(OnDefibrillated);
    }

    /// <summary>
    /// Cap stun duration so permanent effects don't cause infinite stun.
    /// </summary>
    private static readonly TimeSpan MaxStunDuration = TimeSpan.FromSeconds(30);

    private void OnApplied(Entity<CardiacArrestComponent> ent, ref StatusEffectAppliedEvent args)
    {
        EnsureComp<ActiveCardiacArrestComponent>(args.Target);

        if (!TryComp<StatusEffectComponent>(ent, out var effect))
            return;

        var stunDuration = effect.Duration > MaxStunDuration ? MaxStunDuration : effect.Duration;
        _stun.TryAddStunDuration(args.Target, stunDuration);
    }

    private void OnRemoved(Entity<CardiacArrestComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<ActiveCardiacArrestComponent>(args.Target);
    }

    private void OnDefibrillated(Entity<ActiveCardiacArrestComponent> ent, ref TargetDefibrillatedEvent args)
    {
        if (TryComp<StatusEffectContainerComponent>(ent, out var container)
            && container.ActiveStatusEffects is { } effectContainer)
        {
            foreach (var effect in effectContainer.ContainedEntities)
            {
                if (HasComp<CardiacArrestComponent>(effect))
                    QueueDel(effect);
            }
        }

        // Always clean up the active marker so the patient stops suffocating,
        // even if the status effect entity was already gone.
        RemComp<ActiveCardiacArrestComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var drain = -DrainPerSecond * frameTime;
        var query = EntityQueryEnumerator<ActiveCardiacArrestComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            _respirator.UpdateSaturation(uid, drain);
        }
    }
}

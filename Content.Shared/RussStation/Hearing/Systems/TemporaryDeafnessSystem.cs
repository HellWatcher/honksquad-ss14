using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Shared.RussStation.Hearing.Systems;

/// <summary>
/// Bridges the TemporaryDeafness component to the actual body's deafness state. Supports two
/// usage paths:
///
/// 1. Status effect: <see cref="StatusEffectsSystem"/> spawns a child status effect entity
///    carrying <see cref="TemporaryDeafnessComponent"/>; the per-applied event hook adds
///    <see cref="ActiveTemporaryDeafnessComponent"/> to the body. Same shape as
///    <c>BreathingSuppressedSystem</c>.
///
/// 2. Direct attach: tests / one-off cases put <see cref="TemporaryDeafnessComponent"/> right
///    on a body that has <see cref="DeafableComponent"/>; ComponentStartup falls back to
///    treating the host entity as the target.
/// </summary>
public sealed partial class TemporaryDeafnessSystem : EntitySystem
{
    [Dependency] private DeafableSystem _deafable = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemporaryDeafnessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TemporaryDeafnessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TemporaryDeafnessComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<TemporaryDeafnessComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<ActiveTemporaryDeafnessComponent, CanHearAttemptEvent>(OnTryHear);
    }

    private void OnStartup(EntityUid uid, TemporaryDeafnessComponent component, ComponentStartup args)
    {
        // Direct-attach path: only meaningful when the host carries DeafableComponent (a body).
        // The status-effect path goes through OnApplied and uses args.Target instead.
        if (!HasComp<DeafableComponent>(uid))
            return;

        EnsureComp<ActiveTemporaryDeafnessComponent>(uid);
        _deafable.UpdateIsDeaf(uid);
    }

    private void OnShutdown(EntityUid uid, TemporaryDeafnessComponent component, ComponentShutdown args)
    {
        if (!HasComp<DeafableComponent>(uid))
            return;

        RemComp<ActiveTemporaryDeafnessComponent>(uid);
        _deafable.UpdateIsDeaf(uid);
    }

    private void OnApplied(Entity<TemporaryDeafnessComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        EnsureComp<ActiveTemporaryDeafnessComponent>(args.Target);
        _deafable.UpdateIsDeaf(args.Target);
    }

    private void OnRemoved(Entity<TemporaryDeafnessComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<ActiveTemporaryDeafnessComponent>(args.Target);
        _deafable.UpdateIsDeaf(args.Target);
    }

    private void OnTryHear(EntityUid uid, ActiveTemporaryDeafnessComponent component, CanHearAttemptEvent args)
    {
        args.Cancel();
    }
}

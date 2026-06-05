using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.RussStation.EscalatedGrab.Components;
using Content.Shared.RussStation.EscalatedGrab.Events;
using Content.Shared.Strip.Components;
using Content.Shared.Teleportation.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.RussStation.EscalatedGrab.Systems;

/// <summary>
/// Event subscriptions for grab escalation. The stage state machine they drive lives in
/// <c>.Stages.cs</c>; wiring is in the main file's Initialize.
/// </summary>
public abstract partial class SharedEscalatedGrabSystem
{
    private void OnPullerStartCollide(EntityUid uid, GrabStateComponent component, ref StartCollideEvent args)
    {
        if (!HasComp<PortalComponent>(args.OtherEntity))
            return;

        ClearGrabAndStopPull(uid, component);
    }

    private void OnPullableStartCollide(EntityUid uid, PullableComponent component, ref StartCollideEvent args)
    {
        if (!HasComp<PortalComponent>(args.OtherEntity))
            return;

        // Mirror the puller-side handler: clearing escalation alone leaves the pull joint intact,
        // and it can survive the portal's cross-map SetCoordinates and crash on the next physics
        // tick when the engine re-inits a joint spanning two maps (#877). Tear the pull down too.
        if (component.Puller is { } puller && TryComp<GrabStateComponent>(puller, out var state))
            ClearGrabAndStopPull(puller, state);
    }

    private void OnPullerBuckled(EntityUid uid, GrabStateComponent component, ref BuckledEvent args)
    {
        ClearGrabAndStopPull(uid, component);
    }

    private void OnEscalateAttempt(EntityUid uid, PullableComponent component, ref PullGrabEscalateAttemptEvent args)
    {
        if (TryEscalate(args.Puller, args.Pulled))
            args.Handled = true;
    }

    private void OnPullReleaseRequested(EntityUid uid, PullerComponent component, ref PullReleaseRequestedEvent args)
    {
        ClearEscalation(args.Puller);
    }

    private void OnPullableCanMove(EntityUid uid, PullableComponent component, UpdateCanMoveEvent args)
    {
        if (!component.BeingPulled || component.Puller is not { } puller)
            return;

        if (TryComp<GrabStateComponent>(puller, out var state)
            && state.Target == uid
            && state.Stage >= GrabStage.Grab)
        {
            args.Cancel();
        }
    }

    private void OnRefreshPullerSpeed(EntityUid uid, GrabStateComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        var modifier = GrabStateComponent.PullerSpeedModifiers[(int) component.Stage];
        args.ModifySpeed(modifier, modifier);
    }

    /// <summary>
    /// A solid hit on a puller breaks their hold. For an escalated grab this drops one stage;
    /// for a bare vanilla pull it stops the pull outright. Both paths share the same threshold
    /// check, so they live in one handler keyed on whether a <see cref="GrabStateComponent"/> exists.
    /// </summary>
    private void OnDamageCheckDrop(EntityUid uid, PullerComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        var total = args.DamageDelta.GetTotal();

        // Escalated grab: drop one stage once the hit clears the per-grab threshold.
        if (TryComp<GrabStateComponent>(uid, out var state))
        {
            if (total < state.DamageDropThreshold)
                return;

            _popup.PopupEntity(
                Loc.GetString("escalated-grab-broken-by-damage"),
                uid, uid, PopupType.MediumCaution);

            DropStage(uid, state);
            return;
        }

        // Vanilla pull: break the pull on a solid hit so a plain pull can be shaken too.
        if (component.Pulling is not { } pulled)
            return;

        if (total < GrabStateComponent.DefaultDamageDropThreshold)
            return;

        StopPullIfPuller(pulled, uid);
    }

    private void OnTargetBeingStripped(EntityUid uid, PullableComponent component, BeforeGettingStrippedEvent args)
    {
        if (!component.BeingPulled || component.Puller is not { } puller)
            return;

        if (!TryComp<GrabStateComponent>(puller, out var state) || state.Target != uid)
            return;

        args.Multiplier *= GrabStateComponent.StripTimeModifiers[(int) state.Stage];
    }

    private void OnTargetMobStateChanged(EntityUid uid, PullableComponent component, MobStateChangedEvent args)
    {
        // Choke ticks shouldn't keep going on a corpse; release the grab when the target dies.
        // Crit alone doesn't release - you can choke an unconscious target until they die.
        if (args.NewMobState != MobState.Dead)
            return;

        if (component.Puller is not { } puller)
            return;

        if (!TryComp<GrabStateComponent>(puller, out var state) || state.Target != uid)
            return;

        ClearGrabAndStopPull(puller, state);
    }

    private void OnAttemptStopPulling(EntityUid uid, PullableComponent component, ref AttemptStopPullingEvent args)
    {
        if (args.Cancelled || args.User == null)
            return;

        if (component.Puller is not { } puller)
            return;

        if (!TryComp<GrabStateComponent>(puller, out var state) || state.Target != uid)
            return;

        // The puller releasing their own grab always succeeds; resist is target-side only.
        if (args.User == puller)
            return;

        // Anyone other than the grabbed target (e.g. third-party rescuers) goes through normal stop-pull.
        if (args.User != uid)
            return;

        // At Pull stage, no resist needed - let the normal stop-pull proceed.
        if (state.Stage <= GrabStage.Pull)
            return;

        // Cancel the stop-pull and start a resist do-after instead.
        args.Cancelled = true;
        TryResist(uid, puller, state);
    }

    private void OnPullStopped(EntityUid uid, GrabStateComponent component, PullStoppedMessage args)
    {
        // Mutual grab: if A and B are grabbing each other, ending A's pull on B raises
        // PullStoppedMessage on both A and B. B has its own GrabStateComponent for its
        // grab on A, which is unrelated; only clear when this message is for our pull.
        if (args.PullerUid != uid || args.PulledUid != component.Target)
            return;

        ClearEscalation(uid);
    }

    private void OnEscalateDoAfterFinished(EntityUid uid, GrabStateComponent component, GrabEscalateDoAfterEvent args)
    {
        component.EscalateDoAfter = null;
        Dirty(uid, component);

        if (args.Cancelled)
            return;

        if (!TryComp<PullerComponent>(uid, out var puller) || puller.Pulling == null)
            return;

        // Only escalate if still pulling the same target.
        if (puller.Pulling.Value != component.Target)
            return;

        var nextStage = component.Stage + 1;
        if (nextStage > GrabStage.Choke)
            return;

        SetStage(uid, component, nextStage);
    }

    private void OnResistDoAfterFinished(EntityUid uid, PullableComponent component, GrabResistDoAfterEvent args)
    {
        if (component.Puller is not { } puller)
            return;

        if (!TryComp<GrabStateComponent>(puller, out var state))
            return;

        state.ResistDoAfter = null;
        Dirty(puller, state);

        if (args.Cancelled)
            return;

        // Only process if still grabbing the same target.
        if (state.Target != uid)
            return;

        DropStage(puller, state);
    }

    /// <summary>
    /// Clears escalation on <paramref name="puller"/> and stops the underlying pull on its
    /// current target. The shared "clear-on-conflict" shape for portal collisions, the puller
    /// getting buckled, and the target dying.
    /// </summary>
    private void ClearGrabAndStopPull(EntityUid puller, GrabStateComponent state)
    {
        // Capture the target before ClearEscalation removes the component.
        var target = state.Target;
        ClearEscalation(puller);
        StopPullIfPuller(target, puller);
    }
}

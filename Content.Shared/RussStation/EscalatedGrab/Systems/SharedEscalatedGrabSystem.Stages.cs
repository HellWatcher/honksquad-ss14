using Content.Shared.DoAfter;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.RussStation.EscalatedGrab.Components;
using Content.Shared.RussStation.EscalatedGrab.Events;

namespace Content.Shared.RussStation.EscalatedGrab.Systems;

/// <summary>
/// Grab-stage state machine: starting/advancing escalation, resisting, dropping a
/// stage, and tearing escalation down. Event subscriptions live in <c>.Handlers.cs</c>.
/// </summary>
public abstract partial class SharedEscalatedGrabSystem
{
    /// <summary>
    /// Starts a do-after to escalate the grab to the next stage.
    /// If the puller has no escalation yet, the first escalation to Grab is started.
    /// </summary>
    public bool TryEscalate(EntityUid puller, EntityUid target)
    {
        if (!_timing.IsFirstTimePredicted)
            return true;

        var state = EnsureComp<GrabStateComponent>(puller);

        // If already escalating, don't start another.
        if (state.EscalateDoAfter != null)
            return true;

        // Already grabbed a different target, can't escalate.
        if (state.Target != default && state.Target != target)
            return false;

        state.Target = target;
        Dirty(puller, state);

        var nextStage = state.Stage + 1;
        if (nextStage > GrabStage.Choke)
            return true; // Already at max stage.

        var delay = GrabStateComponent.EscalationTimes[(int) nextStage];
        if (delay == TimeSpan.Zero)
        {
            // Instant escalation (shouldn't happen with current config but handle gracefully).
            SetStage(puller, state, nextStage);
            return true;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, puller, delay, new GrabEscalateDoAfterEvent(), puller, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DamageThreshold = 15,
            NeedHand = true,
            DistanceThreshold = 2f,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
        {
            state.EscalateDoAfter = doAfterId;
        }

        return true;
    }

    /// <summary>
    /// Sets the grab stage and raises events/popups.
    /// </summary>
    private void SetStage(EntityUid puller, GrabStateComponent state, GrabStage newStage)
    {
        var oldStage = state.Stage;
        state.Stage = newStage;
        state.ChokeDamageAccumulator = 0f;
        Dirty(puller, state);

        // Popup messages: puller sees recipientMessage, everyone else (including target) sees othersMessage.
        var target = state.Target;
        var localeKey = GrabStateComponent.StagePopupKeys[(int) newStage];

        // Only announce the stage popup on escalation; dropping a stage has its own messaging.
        if (localeKey != null && newStage > oldStage)
        {
            _popup.PopupPredicted(
                Loc.GetString($"{localeKey}-puller", ("target", target)),
                Loc.GetString($"{localeKey}-others", ("puller", puller), ("target", target)),
                target, puller);
        }

        // Refresh movement modifiers for both puller and target.
        _movementSpeed.RefreshMovementSpeedModifiers(puller);
        _actionBlocker.UpdateCanMove(target);

        var ev = new GrabEscalatedEvent(puller, target, oldStage, newStage);
        RaiseLocalEvent(puller, ref ev);
    }

    /// <summary>
    /// Returns the current <see cref="GrabStage"/> for a puller on a target.
    /// Defaults to <see cref="GrabStage.Pull"/> if no escalation exists.
    /// </summary>
    public GrabStage GetStage(EntityUid puller, EntityUid target)
    {
        if (TryComp<GrabStateComponent>(puller, out var comp) && comp.Target == target)
            return comp.Stage;

        return GrabStage.Pull;
    }

    /// <summary>
    /// Checks whether the puller has at least the given grab stage on the target.
    /// </summary>
    public bool HasStage(EntityUid puller, EntityUid target, GrabStage minimumStage)
    {
        return GetStage(puller, target) >= minimumStage;
    }

    /// <summary>
    /// Removes grab escalation from a puller, cancelling any active do-afters.
    /// </summary>
    public void ClearEscalation(EntityUid puller)
    {
        if (!TryComp<GrabStateComponent>(puller, out var state))
            return;

        var target = state.Target;

        // Capture and null the ids before cancelling so the do-after handlers don't see a stale id,
        // and so we don't re-cancel an id the engine already retired (which logs an error).
        var escalateId = state.EscalateDoAfter;
        var resistId = state.ResistDoAfter;
        state.EscalateDoAfter = null;
        state.ResistDoAfter = null;
        Dirty(puller, state);

        if (escalateId != null && _doAfter.GetStatus(escalateId) == DoAfterStatus.Running)
            _doAfter.Cancel(escalateId);
        if (resistId != null && _doAfter.GetStatus(resistId) == DoAfterStatus.Running)
            _doAfter.Cancel(resistId);

        RemComp<GrabStateComponent>(puller);

        // Re-enable target movement and reset puller speed.
        if (target.IsValid())
            _actionBlocker.UpdateCanMove(target);

        _movementSpeed.RefreshMovementSpeedModifiers(puller);
    }

    /// <summary>
    /// Starts a resist do-after for the grabbed target. Drops one stage on completion.
    /// </summary>
    public void TryResist(EntityUid target, EntityUid puller, GrabStateComponent state)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        // Already resisting.
        if (state.ResistDoAfter != null)
            return;

        var resistTime = GrabStateComponent.ResistTimes[(int) state.Stage];

        // Raise attempt event so quirks can modify resist time.
        var attemptEv = new GrabResistAttemptEvent(puller, target, state.Stage, resistTime);
        RaiseLocalEvent(target, ref attemptEv);
        resistTime = attemptEv.ResistTime;

        if (resistTime <= TimeSpan.Zero)
        {
            // Instant resist (e.g. Pull stage, shouldn't reach here but handle gracefully).
            DropStage(puller, state);
            return;
        }

        _popup.PopupPredicted(
            Loc.GetString("escalated-grab-resist-start-target"),
            Loc.GetString("escalated-grab-resist-start-others", ("target", target)),
            target, target);

        var doAfterArgs = new DoAfterArgs(EntityManager, target, resistTime, new GrabResistDoAfterEvent(), target, puller)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = false,
            DistanceThreshold = 2f,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
        {
            state.ResistDoAfter = doAfterId;
            Dirty(puller, state);
        }
    }

    /// <summary>
    /// Drops the grab by one stage. If already at Grab, clears escalation entirely.
    /// </summary>
    public void DropStage(EntityUid puller, GrabStateComponent state)
    {
        var target = state.Target;

        if (state.Stage <= GrabStage.Grab)
        {
            // At Grab or below, fully release - drop both the escalation and the underlying pull.
            ClearEscalation(puller);

            StopPullIfPuller(target, puller);

            if (target.IsValid())
            {
                _popup.PopupPredicted(
                    Loc.GetString("escalated-grab-resist-success-target"),
                    Loc.GetString("escalated-grab-resist-success-others", ("target", target)),
                    target, target);
            }

            return;
        }

        var newStage = state.Stage - 1;
        SetStage(puller, state, newStage);

        if (target.IsValid())
        {
            _popup.PopupPredicted(
                Loc.GetString("escalated-grab-loosened-puller", ("target", target)),
                Loc.GetString("escalated-grab-loosened-others", ("puller", puller), ("target", target)),
                target, puller);
        }
    }

    /// <summary>
    /// Stops the pull on <paramref name="target"/> iff it is currently being pulled by
    /// <paramref name="puller"/>. No-op for an invalid target or a mismatched puller.
    /// </summary>
    private void StopPullIfPuller(EntityUid target, EntityUid puller)
    {
        if (target.IsValid() && TryComp<PullableComponent>(target, out var pullable) && pullable.Puller == puller)
            _pulling.TryStopPull(target, pullable);
    }
}

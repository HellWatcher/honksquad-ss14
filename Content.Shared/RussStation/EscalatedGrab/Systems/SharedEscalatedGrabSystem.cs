using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.RussStation.EscalatedGrab.Components;
using Content.Shared.RussStation.EscalatedGrab.Events;
using Content.Shared.Strip.Components;
using Content.Shared.Teleportation.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.RussStation.EscalatedGrab.Systems;

/// <summary>
/// Manages grab escalation. Re-clicking pull on a target escalates the grab
/// through <see cref="GrabStage"/> tiers via do-afters instead of releasing.
/// </summary>
public abstract class SharedEscalatedGrabSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PullableComponent, PullGrabEscalateAttemptEvent>(OnEscalateAttempt);
        SubscribeLocalEvent<PullerComponent, PullReleaseRequestedEvent>(OnPullReleaseRequested);
        SubscribeLocalEvent<PullableComponent, AttemptStopPullingEvent>(OnAttemptStopPulling);
        SubscribeLocalEvent<PullableComponent, UpdateCanMoveEvent>(OnPullableCanMove);
        SubscribeLocalEvent<GrabStateComponent, PullStoppedMessage>(OnPullStopped);
        SubscribeLocalEvent<GrabStateComponent, GrabEscalateDoAfterEvent>(OnEscalateDoAfterFinished);
        SubscribeLocalEvent<GrabStateComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshPullerSpeed);
        SubscribeLocalEvent<PullableComponent, GrabResistDoAfterEvent>(OnResistDoAfterFinished);
        SubscribeLocalEvent<GrabStateComponent, DamageChangedEvent>(OnPullerDamaged);
        SubscribeLocalEvent<PullerComponent, DamageChangedEvent>(OnVanillaPullerDamaged);
        SubscribeLocalEvent<PullableComponent, BeforeGettingStrippedEvent>(OnTargetBeingStripped);

        // Portal interactions: force-break the pull before the portal teleports either side.
        // Upstream's SharedPortalSystem already calls TryStopPull on portal collision, but the
        // joint can survive a re-prediction race when escalated grab keeps the pair tethered
        // longer than vanilla pulls. We pre-empt the teleport here so the joint is fully torn
        // down before SetCoordinates runs and the next physics tick tries to re-init it cross-map.
        SubscribeLocalEvent<GrabStateComponent, StartCollideEvent>(OnPullerStartCollide);
        SubscribeLocalEvent<PullableComponent, StartCollideEvent>(OnPullableStartCollide);

        // Puller getting buckled: drop escalation and stop the pull so the puller's joint
        // doesn't get reparented onto the strap. (The pulled-getting-buckled side is already
        // handled by upstream's PullingSystem.OnGotBuckled, which calls StopPulling and
        // therefore fires PullStoppedMessage into our OnPullStopped clean-up path.)
        SubscribeLocalEvent<GrabStateComponent, BuckledEvent>(OnPullerBuckled);
    }

    private void OnPullerStartCollide(EntityUid uid, GrabStateComponent component, ref StartCollideEvent args)
    {
        if (!HasComp<PortalComponent>(args.OtherEntity))
            return;

        var target = component.Target;
        ClearEscalation(uid);

        if (target.IsValid() && TryComp<PullableComponent>(target, out var pullable) && pullable.Puller == uid)
            _pulling.TryStopPull(target, pullable);
    }

    private void OnPullableStartCollide(EntityUid uid, PullableComponent component, ref StartCollideEvent args)
    {
        if (!HasComp<PortalComponent>(args.OtherEntity))
            return;

        if (component.Puller is { } puller && HasComp<GrabStateComponent>(puller))
            ClearEscalation(puller);
    }

    private void OnPullerBuckled(EntityUid uid, GrabStateComponent component, ref BuckledEvent args)
    {
        var target = component.Target;
        ClearEscalation(uid);

        if (target.IsValid() && TryComp<PullableComponent>(target, out var pullable) && pullable.Puller == uid)
            _pulling.TryStopPull(target, pullable);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<GrabStateComponent>();
        while (query.MoveNext(out var uid, out var state))
        {
            if (state.Stage != GrabStage.Choke)
                continue;

            if (!state.Target.IsValid())
                continue;

            state.ChokeDamageAccumulator += frameTime;
            while (state.ChokeDamageAccumulator >= state.ChokeTickInterval)
            {
                state.ChokeDamageAccumulator -= state.ChokeTickInterval;
                _stamina.TakeStaminaDamage(state.Target, state.ChokeStaminaPerTick, source: uid);
            }
        }
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

    private void OnPullerDamaged(EntityUid uid, GrabStateComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (args.DamageDelta == null)
            return;

        var total = args.DamageDelta.GetTotal();
        if (total < component.DamageDropThreshold)
            return;

        _popup.PopupEntity(
            Loc.GetString("escalated-grab-broken-by-damage"),
            uid, uid, PopupType.MediumCaution);

        DropStage(uid, component);
    }

    private void OnVanillaPullerDamaged(EntityUid uid, PullerComponent component, DamageChangedEvent args)
    {
        // Escalated grabs are handled by OnPullerDamaged; this only covers vanilla pulls so a
        // pull on its own can also be broken by a solid hit.
        if (HasComp<GrabStateComponent>(uid))
            return;

        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        if (component.Pulling is not { } pulled)
            return;

        if (args.DamageDelta.GetTotal() < GrabStateComponent.DefaultDamageDropThreshold)
            return;

        if (TryComp<PullableComponent>(pulled, out var pullable))
            _pulling.TryStopPull(pulled, pullable);
    }

    private void OnTargetBeingStripped(EntityUid uid, PullableComponent component, BeforeGettingStrippedEvent args)
    {
        if (!component.BeingPulled || component.Puller is not { } puller)
            return;

        if (!TryComp<GrabStateComponent>(puller, out var state) || state.Target != uid)
            return;

        args.Multiplier *= GrabStateComponent.StripTimeModifiers[(int) state.Stage];
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
        var localeKey = newStage switch
        {
            GrabStage.Grab => "escalated-grab-grab",
            GrabStage.Aggressive => "escalated-grab-aggressive",
            GrabStage.Choke => "escalated-grab-choke",
            _ => null,
        };

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
        }
    }

    private void OnResistDoAfterFinished(EntityUid uid, PullableComponent component, GrabResistDoAfterEvent args)
    {
        if (component.Puller is not { } puller)
            return;

        if (!TryComp<GrabStateComponent>(puller, out var state))
            return;

        state.ResistDoAfter = null;

        if (args.Cancelled)
            return;

        // Only process if still grabbing the same target.
        if (state.Target != uid)
            return;

        DropStage(puller, state);
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

            if (target.IsValid() && TryComp<PullableComponent>(target, out var pullable) && pullable.Puller == puller)
                _pulling.TryStopPull(target, pullable);

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
}

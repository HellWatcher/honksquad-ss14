using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Mobs;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.RussStation.EscalatedGrab.Components;
using Content.Shared.RussStation.EscalatedGrab.Events;
using Content.Shared.Strip.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.RussStation.EscalatedGrab.Systems;

/// <summary>
/// Manages grab escalation. Re-clicking pull on a target escalates the grab
/// through <see cref="GrabStage"/> tiers via do-afters instead of releasing.
/// </summary>
/// <remarks>
/// Split across partial files: this file holds the lifecycle (Initialize/Update);
/// <c>.Stages.cs</c> holds the stage state machine (escalate/resist/drop/clear);
/// <c>.Handlers.cs</c> holds the event subscriptions.
/// </remarks>
public abstract partial class SharedEscalatedGrabSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private PullingSystem _pulling = default!;

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

        // One handler covers both escalated grabs and bare vanilla pulls; it branches on
        // whether the puller carries a GrabStateComponent. PullerComponent is present in
        // both cases (an escalated grab is layered on top of a pull).
        SubscribeLocalEvent<PullerComponent, DamageChangedEvent>(OnDamageCheckDrop);

        SubscribeLocalEvent<PullableComponent, BeforeGettingStrippedEvent>(OnTargetBeingStripped);
        SubscribeLocalEvent<PullableComponent, MobStateChangedEvent>(OnTargetMobStateChanged);

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
}

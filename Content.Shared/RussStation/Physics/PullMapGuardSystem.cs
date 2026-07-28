// HONK - Break any joint whose endpoints end up on different maps.
// Without this, FTL-reparenting one side of a joint (e.g. arrivals grid
// returning from the FTL map) leaves the joint cross-map, which trips
// the engine's cross-map joint assert during client prediction.
// Covers pulling, carrying, buckle relay, grappling gun - any JointComponent.
//
// Also mirrors the check via PullerComponent/PullableComponent, because
// the pull joint can be pending in SharedJointSystem.AddedJoints (or a
// state rollback) before it lands in JointComponent.Joints, in which
// case the JointComponent-only path misses the transition (e.g. a
// pullable walking into a portal while the joint is mid-init).
//
// Per-tick sweep: the EntParentChanged handlers can miss the case where
// one body's transform arrives from server state on a later tick than
// the joint state (the engine defers such joints into AddedJoints, and
// our parent-changed handler ran before the second transform landed).
// The sweep runs UpdatesBefore SharedJointSystem.Update so divergent
// pulls are torn down before InitJoint trips its cross-map assert.
//
// Deferred-joint drain: prediction resets can re-add a stale
// JointComponent onto a PVS-detached entity (the re-add-predicted-
// removals path in ClientGameStateManager skips the Detached check),
// re-queueing a long-dead pull joint into the engine-private
// SharedJointSystem.AddedJoints while the pull link is null on both
// sides, so no link- or event-keyed teardown can see it. That re-add
// always creates a fresh JointComponent (subscribing to the component's
// ComponentHandleState directly is impossible - the engine's client
// JointSystem holds that subscription exclusively), so we arm on
// ComponentStartup and one-shot drain armed owners that sit in
// nullspace, via ClearJoints - the one public API that also drains
// AddedJoints - before SharedJointSystem inits them with no map
// re-check.
using System.Collections.Generic;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.RussStation.Physics;

public sealed partial class PullMapGuardSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private EntityQuery<PullableComponent> _pullableQuery = default!;

    private readonly List<Joint> _toBreak = new();

    // Entities that just gained a JointComponent, checked once in the next Update and then
    // forgotten. Client-only: the crash feeder (stale state re-added onto a detached entity)
    // is client state machinery, and a fresh server-side JointComponent in nullspace has no
    // joints to drain anyway.
    private readonly HashSet<EntityUid> _freshJointComps = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(SharedJointSystem));
        SubscribeLocalEvent<JointComponent, EntParentChangedMessage>(OnJointParentChanged);
        SubscribeLocalEvent<JointComponent, ComponentStartup>(OnJointStartup);
        SubscribeLocalEvent<PullerComponent, EntParentChangedMessage>(OnPullerParentChanged);
        SubscribeLocalEvent<PullableComponent, EntParentChangedMessage>(OnPullableParentChanged);

    }

    private void OnJointStartup(Entity<JointComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            _freshJointComps.Add(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Active pulls: tear down any pull whose endpoints now sit on different maps.
        // We deliberately do NOT scan all JointComponents here - that approach feedback-loops
        // with state replication when the server has a stuck cross-map joint and floods the
        // log with ClearJoints calls every tick.
        var pullerQuery = EntityQueryEnumerator<PullerComponent>();
        while (pullerQuery.MoveNext(out var uid, out var puller))
        {
            if (puller.Pulling is not { } pulled)
                continue;

            if (!MapsDiverged(uid, pulled))
                continue;

            if (_pullableQuery.TryGetComponent(pulled, out var pullable))
                _pulling.TryStopPull(pulled, pullable);

            _joints.ClearJoints(pulled);
            _joints.ClearJoints(uid);
        }

        // One-shot check on freshly added JointComponents. The one that matters: a stale pull
        // joint resurrected onto a PVS-detached pullee by the prediction reset (see header) -
        // the engine defers it into AddedJoints because the pullee's transform reads
        // Nullspace, and the drain in SharedJointSystem.Update, which runs right after us,
        // inits it with no map re-check, tripping the cross-map assert. A joint on a
        // nullspace entity can never legally init this tick (AddJoint itself refuses
        // nullspace), so drain it; owners on real maps are legitimate joint setups and pass
        // untouched. The set is cleared every Update, so this does nothing at all on ticks
        // without new joint components, and fires at most once per component addition -
        // repeating it every tick would spam the engine's ClearJoints debug log.
        if (_freshJointComps.Count > 0)
        {
            foreach (var uid in _freshJointComps)
            {
                if (TerminatingOrDeleted(uid))
                    continue;

                if (_transform.GetMapId(uid) != MapId.Nullspace)
                    continue;

                _joints.ClearJoints(uid);
            }

            _freshJointComps.Clear();
        }
    }

    private void OnJointParentChanged(Entity<JointComponent> ent, ref EntParentChangedMessage args)
    {
        if (ent.Comp.GetJoints.Count == 0)
            return;

        var map = _transform.GetMapId(ent.Owner);

        foreach (var joint in ent.Comp.GetJoints.Values)
        {
            var other = joint.BodyAUid == ent.Owner ? joint.BodyBUid : joint.BodyAUid;
            if (_transform.GetMapId(other) != map)
                _toBreak.Add(joint);
        }

        if (_toBreak.Count == 0)
            return;

        foreach (var joint in _toBreak)
            _joints.RemoveJoint(joint);

        _toBreak.Clear();
    }

    private void OnPullerParentChanged(Entity<PullerComponent> ent, ref EntParentChangedMessage args)
    {
        if (ent.Comp.Pulling is not { } pullable)
            return;

        if (!MapsDiverged(ent.Owner, pullable))
            return;

        if (TryComp<PullableComponent>(pullable, out var pullableComp))
            _pulling.TryStopPull(pullable, pullableComp);

        DrainPendingJoints(ent.Owner, pullable);
    }

    private void OnPullableParentChanged(Entity<PullableComponent> ent, ref EntParentChangedMessage args)
    {
        if (ent.Comp.Puller is not { } puller)
            return;

        if (!MapsDiverged(ent.Owner, puller))
            return;

        _pulling.TryStopPull(ent.Owner, ent.Comp);
        DrainPendingJoints(ent.Owner, puller);
    }

    /// <summary>
    /// Removes both endpoints' joints, including any deferred ones in <c>SharedJointSystem.AddedJoints</c>.
    /// Plain <c>RemoveJoint(uid, id)</c> only touches <c>JointComponent.Joints</c>, so a joint that arrived
    /// via state replication while one transform was Nullspace can outlive the pull state that birthed it
    /// and still reach <c>InitJoint</c> the next tick. <c>ClearJoints</c> drains both sets.
    /// </summary>
    private void DrainPendingJoints(EntityUid a, EntityUid b)
    {
        if (HasComp<JointComponent>(a))
            _joints.ClearJoints(a);
        if (HasComp<JointComponent>(b))
            _joints.ClearJoints(b);
    }

    private bool MapsDiverged(EntityUid a, EntityUid b)
    {
        // Treat Nullspace-vs-real as divergent too. The engine's deferred-joint path
        // (JointComponent state arriving while one body's transform is still Nullspace)
        // queues the joint into AddedJoints, where the next physics tick's InitJoint
        // asserts on the cross-map comparison once the transform lands. We need to
        // tear down before that happens, even if one side is currently Nullspace.
        var mapA = _transform.GetMapId(a);
        var mapB = _transform.GetMapId(b);
        return mapA != mapB;
    }
}

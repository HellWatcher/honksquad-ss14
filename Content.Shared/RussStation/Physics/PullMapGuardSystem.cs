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
// Client nullspace drain: prediction resets can re-add a stale
// JointComponent onto a PVS-detached entity (the re-add-predicted-
// removals path in ClientGameStateManager skips the Detached check),
// re-queueing a long-dead pull joint into AddedJoints while the pull
// link is null on both sides. See Update for the full story.
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

public sealed class PullMapGuardSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly EntityQuery<PullableComponent> _pullableQuery = default!;

    private readonly List<Joint> _toBreak = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(SharedJointSystem));
        SubscribeLocalEvent<JointComponent, EntParentChangedMessage>(OnJointParentChanged);
        SubscribeLocalEvent<PullerComponent, EntParentChangedMessage>(OnPullerParentChanged);
        SubscribeLocalEvent<PullableComponent, EntParentChangedMessage>(OnPullableParentChanged);

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

        // Client only: drain joints the engine deferred into SharedJointSystem.AddedJoints
        // while their owner sat detached in nullspace. Prediction resets can resurrect a stale
        // JointComponent (last server state from before the pull broke - the pullee left PVS
        // before the joint-removal state could arrive) onto a PVS-detached pullee via the
        // "re-add predicted removals" path, which skips the Detached check. The client engine
        // defers those joints into AddedJoints because the transform reads Nullspace, then
        // inits them on the next tick with no map re-check, tripping the cross-map assert.
        // By then the pull link is already null on BOTH sides, so the link sweep above and the
        // parent-changed handlers below can't see it. ClearJoints is the only public API that
        // drains AddedJoints; on a nullspace entity with an empty Joints dict it is a pure
        // drain with no side effects. A joint on a nullspace entity can never legally init
        // anyway (AddJoint itself refuses nullspace), and legitimately re-entering entities
        // have left nullspace by the time this runs, so they are never touched.
        // AllEntityQuery, not EntityQueryEnumerator: PVS-detached entities are marked paused
        // (ClientGameStateManager.Detach does this without raising a pause event), and the
        // plain enumerator skips paused entities - which is exactly the population this sweep
        // exists to visit.
        if (_net.IsClient)
        {
            var jointQuery = AllEntityQuery<JointComponent, TransformComponent>();
            while (jointQuery.MoveNext(out var uid, out var joint, out var xform))
            {
                if (xform.MapID != MapId.Nullspace)
                    continue;

                if (!ShouldDrainDetached(uid, joint))
                    continue;

                _joints.ClearJoints(uid, joint);
            }
        }
    }

    /// <summary>
    /// Whether a nullspace entity's joints should be drained. Co-detached pairs (both
    /// endpoints in nullspace, e.g. two jointed entities that left PVS together) keep their
    /// stale-but-consistent data so PVS re-entry can reconcile it; anything else - an empty
    /// dict possibly hiding a pending <c>AddedJoints</c> entry, or a dict joint whose other
    /// endpoint is on a real map - gets cleared before it can init cross-map.
    /// </summary>
    private bool ShouldDrainDetached(EntityUid uid, JointComponent joint)
    {
        foreach (var j in joint.GetJoints.Values)
        {
            var other = j.BodyAUid == uid ? j.BodyBUid : j.BodyAUid;
            if (_transform.GetMapId(other) == MapId.Nullspace)
                return false;
        }

        return true;
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

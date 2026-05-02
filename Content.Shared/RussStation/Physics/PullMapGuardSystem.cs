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
using System.Collections.Generic;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.RussStation.Physics;

public sealed class PullMapGuardSystem : EntitySystem
{
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

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

        // Pass 1 - active pulls: tear down any pull whose endpoints now sit on different maps.
        var pullerQuery = EntityQueryEnumerator<PullerComponent>();
        while (pullerQuery.MoveNext(out var uid, out var puller))
        {
            if (puller.Pulling is not { } pulled)
                continue;

            if (!MapsDiverged(uid, pulled))
                continue;

            if (TryComp<PullableComponent>(pulled, out var pullable))
                _pulling.TryStopPull(pulled, pullable);

            _joints.ClearJoints(pulled);
            _joints.ClearJoints(uid);
        }

        // Pass 2 - orphaned joints: a joint can outlive its pull state when it was deferred
        // into SharedJointSystem.AddedJoints during state replication (transform was Nullspace
        // when the joint state arrived) and the matching pull was broken before the deferred
        // joint got drained. The next physics tick processes AddedJoints via InitJoint, which
        // asserts cross-map. We can't enumerate AddedJoints from outside the engine, but
        // ClearJoints does sweep it for the targeted entity, so walk every JointComponent and
        // clear ones whose own committed joints already point cross-map. The same call will
        // drop any same-pair entries waiting in AddedJoints.
        var jointQuery = EntityQueryEnumerator<JointComponent>();
        while (jointQuery.MoveNext(out var uid, out var jointComp))
        {
            if (jointComp.GetJoints.Count == 0)
                continue;

            var myMap = _transform.GetMapId(uid);
            var diverged = false;
            foreach (var joint in jointComp.GetJoints.Values)
            {
                var other = joint.BodyAUid == uid ? joint.BodyBUid : joint.BodyAUid;
                if (_transform.GetMapId(other) != myMap)
                {
                    diverged = true;
                    break;
                }
            }

            if (diverged)
                _joints.ClearJoints(uid);
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

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

        var query = EntityQueryEnumerator<PullerComponent>();
        while (query.MoveNext(out var uid, out var puller))
        {
            if (puller.Pulling is not { } pulled)
                continue;

            if (!MapsDiverged(uid, pulled))
                continue;

            // Tear down both the high-level pull state and the joints themselves.
            // ClearJoints sweeps SharedJointSystem.AddedJoints too, which is the
            // path that the EntParentChanged handlers can't reach for joints that
            // arrived via server state replication mid-teleport.
            if (TryComp<PullableComponent>(pulled, out var pullable))
                _pulling.TryStopPull(pulled, pullable);

            _joints.ClearJoints(pulled);
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
    }

    private void OnPullableParentChanged(Entity<PullableComponent> ent, ref EntParentChangedMessage args)
    {
        if (ent.Comp.Puller is not { } puller)
            return;

        if (!MapsDiverged(ent.Owner, puller))
            return;

        _pulling.TryStopPull(ent.Owner, ent.Comp);
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

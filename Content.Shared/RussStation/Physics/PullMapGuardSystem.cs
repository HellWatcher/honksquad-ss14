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
        SubscribeLocalEvent<JointComponent, EntParentChangedMessage>(OnJointParentChanged);
        SubscribeLocalEvent<PullerComponent, EntParentChangedMessage>(OnPullerParentChanged);
        SubscribeLocalEvent<PullableComponent, EntParentChangedMessage>(OnPullableParentChanged);
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
        var mapA = _transform.GetMapId(a);
        var mapB = _transform.GetMapId(b);
        return mapA != mapB && mapA != MapId.Nullspace && mapB != MapId.Nullspace;
    }
}

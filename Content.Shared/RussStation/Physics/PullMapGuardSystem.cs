// HONK - Break any joint whose endpoints end up on different maps.
// Without this, FTL-reparenting one side of a joint (e.g. arrivals grid
// returning from the FTL map) leaves the joint cross-map, which trips
// the engine's cross-map joint assert during client prediction.
// Covers pulling, carrying, buckle relay, grappling gun — any JointComponent.
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.RussStation.Physics;

public sealed class PullMapGuardSystem : EntitySystem
{
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<Joint> _toBreak = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JointComponent, EntParentChangedMessage>(OnParentChanged);
    }

    private void OnParentChanged(Entity<JointComponent> ent, ref EntParentChangedMessage args)
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
}

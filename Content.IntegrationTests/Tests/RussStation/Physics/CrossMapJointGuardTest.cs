using Content.Shared.RussStation.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.RussStation.Physics;

/// <summary>
/// Regression tests for <see cref="PullMapGuardSystem"/>. Engine asserts in
/// <c>SharedJointSystem.InitJoint</c> when a joint ends up spanning two different
/// maps (typical cause: arrivals FTL reparenting a body while it's still joined
/// to something on the station map). The guard watches <c>EntParentChangedMessage</c>
/// on <see cref="JointComponent"/> and breaks any joint whose endpoints aren't on
/// the same map anymore. These tests cover the positive case (joint broken),
/// same-map reparents (no-op), and multiple joints where only the cross-map ones
/// should be dropped.
/// </summary>
[TestFixture]
[TestOf(typeof(PullMapGuardSystem))]
public sealed class CrossMapJointGuardTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: JointGuardBody
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fix1:
        shape: !type:PhysShapeCircle
          radius: 0.25
";

    /// <summary>
    /// Reparenting one endpoint of a joint to a different map must cause the guard
    /// to remove that joint before the next physics tick tries to initialize it.
    /// </summary>
    [Test]
    public async Task JointBreaksWhenEndpointCrossesMaps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var joints = entMan.System<SharedJointSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapA);
            mapSys.CreateMap(out var mapB);

            var a = entMan.SpawnEntity("JointGuardBody", new MapCoordinates(0, 0, mapA));
            var b = entMan.SpawnEntity("JointGuardBody", new MapCoordinates(0, 0, mapA));

            var joint = joints.CreateDistanceJoint(a, b, id: "cross-map-test");
            Assert.That(entMan.TryGetComponent<JointComponent>(a, out var aJoints));
            Assert.That(aJoints!.GetJoints, Has.Count.EqualTo(1),
                "Joint should exist on same-map spawn");

            var mapBEnt = mapSys.GetMap(mapB);
            xformSys.SetParent(b, mapBEnt);

            // Guard subscribes to EntParentChangedMessage and runs synchronously
            // in the same event chain, so the joint should already be gone.
            entMan.TryGetComponent<JointComponent>(a, out var aJointsAfter);
            var aCount = aJointsAfter?.GetJoints.Count ?? 0;
            entMan.TryGetComponent<JointComponent>(b, out var bJointsAfter);
            var bCount = bJointsAfter?.GetJoints.Count ?? 0;

            Assert.That(aCount, Is.Zero, "Cross-map joint should have been removed from endpoint A");
            Assert.That(bCount, Is.Zero, "Cross-map joint should have been removed from endpoint B");

            // Cleanup: the test map pool is picky about leftover maps.
            entMan.DeleteEntity(mapSys.GetMap(mapA));
            entMan.DeleteEntity(mapBEnt);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Reparenting within the same map must not disturb the joint. This locks in
    /// that the guard's MapId check is load-bearing and doesn't over-fire.
    /// </summary>
    [Test]
    public async Task JointSurvivesSameMapReparent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var joints = entMan.System<SharedJointSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapA);

            var a = entMan.SpawnEntity("JointGuardBody", new MapCoordinates(0, 0, mapA));
            var b = entMan.SpawnEntity("JointGuardBody", new MapCoordinates(1, 0, mapA));
            var holder = entMan.SpawnEntity("JointGuardBody", new MapCoordinates(2, 0, mapA));

            joints.CreateDistanceJoint(a, b, id: "same-map-test");

            xformSys.SetParent(b, holder);

            Assert.That(entMan.TryGetComponent<JointComponent>(a, out var aJoints));
            Assert.That(aJoints!.GetJoints, Has.Count.EqualTo(1),
                "Joint should still exist after same-map reparent");

            entMan.DeleteEntity(mapSys.GetMap(mapA));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// When an entity holds multiple joints and only some cross maps after reparent,
    /// only the cross-map ones should be dropped. Protects against a regression that
    /// nukes all joints on any reparent.
    /// </summary>
    [Test]
    public async Task OnlyCrossMapJointsAreBroken()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var joints = entMan.System<SharedJointSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapA);
            mapSys.CreateMap(out var mapB);

            // center holds joints to same-map partner and cross-map partner.
            var center = entMan.SpawnEntity("JointGuardBody", new MapCoordinates(0, 0, mapA));
            var sameMap = entMan.SpawnEntity("JointGuardBody", new MapCoordinates(1, 0, mapA));
            var crossMap = entMan.SpawnEntity("JointGuardBody", new MapCoordinates(0, 0, mapA));

            joints.CreateDistanceJoint(center, sameMap, id: "same");
            joints.CreateDistanceJoint(center, crossMap, id: "cross");

            Assert.That(entMan.GetComponent<JointComponent>(center).GetJoints, Has.Count.EqualTo(2));

            xformSys.SetParent(crossMap, mapSys.GetMap(mapB));

            var centerJoints = entMan.GetComponent<JointComponent>(center).GetJoints;
            Assert.That(centerJoints, Has.Count.EqualTo(1),
                "Only the cross-map joint should be removed; the same-map joint must survive");
            Assert.That(centerJoints.ContainsKey("same"), Is.True,
                "Same-map joint should still be present by id");

            entMan.DeleteEntity(mapSys.GetMap(mapA));
            entMan.DeleteEntity(mapSys.GetMap(mapB));
        });

        await pair.CleanReturnAsync();
    }
}

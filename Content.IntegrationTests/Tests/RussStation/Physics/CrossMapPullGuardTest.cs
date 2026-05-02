using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.RussStation.EscalatedGrab;
using Content.Shared.RussStation.EscalatedGrab.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests.RussStation.Physics;

/// <summary>
/// End-to-end regression for the same failure class as <see cref="CrossMapJointGuardTest"/>,
/// but routed through <see cref="PullingSystem"/>. Pulls use a distance joint under the hood,
/// so when arrivals FTL reparents a puller or pullable across maps, the joint guard breaks
/// the joint — but the pull's higher-level state (<see cref="PullerComponent.Pulling"/> and
/// <see cref="PullableComponent.Puller"/>) has to clean up from the <c>JointRemovedEvent</c>
/// cascade, or we leak a dangling relationship.
/// </summary>
[TestFixture]
[TestOf(typeof(PullingSystem))]
public sealed class CrossMapPullGuardTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CrossMapPullBody
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fix1:
        shape: !type:PhysShapeCircle
          radius: 0.25
  - type: Puller
    needsHands: false
  - type: Pullable
";

    /// <summary>
    /// Start a pull, then reparent the pullable to a different map. The joint guard
    /// breaks the joint; pulling state must follow it down rather than dangle.
    /// </summary>
    [Test]
    public async Task PullStateClearsWhenPullableCrossesMaps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var pulling = entMan.System<PullingSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapA);
            mapSys.CreateMap(out var mapB);

            var puller = entMan.SpawnEntity("CrossMapPullBody", new MapCoordinates(0, 0, mapA));
            var pullable = entMan.SpawnEntity("CrossMapPullBody", new MapCoordinates(0.5f, 0, mapA));

            Assert.That(pulling.TryStartPull(puller, pullable), Is.True, "Pull should start on same-map entities");
            Assert.That(entMan.GetComponent<PullerComponent>(puller).Pulling, Is.EqualTo(pullable));
            Assert.That(entMan.GetComponent<PullableComponent>(pullable).Puller, Is.EqualTo(puller));

            xformSys.SetParent(pullable, mapSys.GetMap(mapB));

            Assert.That(entMan.GetComponent<PullerComponent>(puller).Pulling, Is.Null,
                "Puller-side pull state should clear after cross-map reparent");
            Assert.That(entMan.GetComponent<PullableComponent>(pullable).Puller, Is.Null,
                "Pullable-side pull state should clear after cross-map reparent");

            entMan.DeleteEntity(mapSys.GetMap(mapA));
            entMan.DeleteEntity(mapSys.GetMap(mapB));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Symmetric: reparent the puller across maps instead. The joint lives on the
    /// pullable via <c>JointComponent</c>, so the guard path differs slightly.
    /// </summary>
    [Test]
    public async Task PullStateClearsWhenPullerCrossesMaps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var pulling = entMan.System<PullingSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapA);
            mapSys.CreateMap(out var mapB);

            var puller = entMan.SpawnEntity("CrossMapPullBody", new MapCoordinates(0, 0, mapA));
            var pullable = entMan.SpawnEntity("CrossMapPullBody", new MapCoordinates(0.5f, 0, mapA));

            Assert.That(pulling.TryStartPull(puller, pullable), Is.True);

            xformSys.SetParent(puller, mapSys.GetMap(mapB));

            Assert.That(entMan.GetComponent<PullerComponent>(puller).Pulling, Is.Null);
            Assert.That(entMan.GetComponent<PullableComponent>(pullable).Puller, Is.Null);

            entMan.DeleteEntity(mapSys.GetMap(mapA));
            entMan.DeleteEntity(mapSys.GetMap(mapB));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Escalated-grab counterpart to <see cref="PullStateClearsWhenPullableCrossesMaps"/>:
    /// the joint guard already breaks the underlying pull on cross-map reparent, but the
    /// fork-side <see cref="GrabStateComponent"/> has to fall along with it via the
    /// <c>PullStoppedMessage</c> cascade. The full shuttle-FTL case (grid reparents while
    /// mobs keep grid as their parent) isn't reachable through plain SetParent in tests
    /// — the engine's grid-map propagation has its own pathway — but the direct reparent
    /// still exercises the grab cleanup path that FTL ultimately depends on.
    /// </summary>
    [Test]
    public async Task EscalatedGrabClearsWhenPairCrossesMaps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var pulling = entMan.System<PullingSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapA);
            mapSys.CreateMap(out var mapB);

            var puller = entMan.SpawnEntity("CrossMapPullBody", new MapCoordinates(0, 0, mapA));
            var pullable = entMan.SpawnEntity("CrossMapPullBody", new MapCoordinates(0.5f, 0, mapA));

            Assert.That(pulling.TryStartPull(puller, pullable), Is.True);

            var grab = entMan.EnsureComponent<GrabStateComponent>(puller);
            grab.Target = pullable;
            grab.Stage = GrabStage.Aggressive;

            xformSys.SetParent(pullable, mapSys.GetMap(mapB));

            Assert.That(entMan.GetComponent<PullerComponent>(puller).Pulling, Is.Null,
                "Pull should be torn down after the pullable crosses maps");
            Assert.That(entMan.HasComponent<GrabStateComponent>(puller), Is.False,
                "Escalated grab state should clear when its underlying pull breaks");

            entMan.DeleteEntity(mapSys.GetMap(mapA));
            entMan.DeleteEntity(mapSys.GetMap(mapB));
        });

        await pair.CleanReturnAsync();
    }
}

using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.RussStation.Pulling;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests.RussStation.Pulling;

/// <summary>
/// Verifies <see cref="VirtualItemPullReleaseSystem"/>: dropping the virtual item that
/// represents a pulled entity stops the pull. A virtual item whose blocking entity is not the
/// thing being pulled leaves the pull alone.
/// </summary>
[TestFixture]
[TestOf(typeof(VirtualItemPullReleaseSystem))]
public sealed class VirtualItemPullReleaseTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: VirtualPullBody
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

    [Test]
    public async Task DroppingVirtualItemStopsPullTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var pulling = entMan.System<PullingSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var puller = entMan.SpawnEntity("VirtualPullBody", new MapCoordinates(0, 0, mapId));
            var pullable = entMan.SpawnEntity("VirtualPullBody", new MapCoordinates(0.5f, 0, mapId));

            Assert.That(pulling.TryStartPull(puller, pullable), Is.True);
            Assert.That(entMan.GetComponent<PullerComponent>(puller).Pulling, Is.EqualTo(pullable));

            // The virtual item stands in for the pulled entity in the puller's hand.
            var virtualItem = entMan.SpawnEntity(null, new MapCoordinates(0, 0, mapId));
            var virtualComp = entMan.AddComponent<VirtualItemComponent>(virtualItem);
            virtualComp.BlockingEntity = pullable;

            entMan.EventBus.RaiseLocalEvent(virtualItem, new DroppedEvent(puller));

            Assert.That(entMan.GetComponent<PullerComponent>(puller).Pulling, Is.Null,
                "Dropping the virtual item should stop the pull.");
            Assert.That(entMan.GetComponent<PullableComponent>(pullable).Puller, Is.Null);

            entMan.DeleteEntity(mapSys.GetMap(mapId));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DroppingUnrelatedVirtualItemKeepsPullTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var pulling = entMan.System<PullingSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var puller = entMan.SpawnEntity("VirtualPullBody", new MapCoordinates(0, 0, mapId));
            var pullable = entMan.SpawnEntity("VirtualPullBody", new MapCoordinates(0.5f, 0, mapId));
            var unrelated = entMan.SpawnEntity("VirtualPullBody", new MapCoordinates(1f, 0, mapId));

            Assert.That(pulling.TryStartPull(puller, pullable), Is.True);

            // A virtual item blocking some other entity must not tear down this pull.
            var virtualItem = entMan.SpawnEntity(null, new MapCoordinates(0, 0, mapId));
            var virtualComp = entMan.AddComponent<VirtualItemComponent>(virtualItem);
            virtualComp.BlockingEntity = unrelated;

            entMan.EventBus.RaiseLocalEvent(virtualItem, new DroppedEvent(puller));

            Assert.That(entMan.GetComponent<PullerComponent>(puller).Pulling, Is.EqualTo(pullable),
                "An unrelated virtual item drop should leave the pull intact.");

            entMan.DeleteEntity(mapSys.GetMap(mapId));
        });

        await pair.CleanReturnAsync();
    }
}

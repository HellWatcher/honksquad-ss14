using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Light.EntitySystems;
using Content.Server.RussStation.Light;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.RussStation.Light;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Light;

/// <summary>
/// Covers <see cref="LightReplacerRecyclerSystem"/>: recycle-point accounting, the storage cap,
/// and the three-tier replacement strategy (exact prototype match, same-type fallback, print)
/// plus the no-replacement-possible case. The replace flow is driven through the same
/// <see cref="LightReplacerRecycleReplaceEvent"/> the base light replacer raises, so these
/// exercise the real selection and execution code rather than reimplementing it.
/// </summary>
[TestOf(typeof(LightReplacerRecyclerSystem))]
public sealed class LightReplacerRecyclerTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestRecyclerReplacer
  components:
  - type: LightReplacer
  - type: LightReplacerRecycler
  - type: Transform

- type: entity
  id: TestRecyclerFixture
  components:
  - type: PoweredLight
    bulb: Bulb
  - type: Transform

- type: entity
  id: TestRecyclerBulbA
  components:
  - type: LightBulb
    bulb: Bulb
  - type: Transform

- type: entity
  id: TestRecyclerBulbB
  components:
  - type: LightBulb
    bulb: Bulb
  - type: Transform

- type: entity
  id: TestRecyclerShard
  components:
  - type: Tag
    tags:
    - GlassShard
  - type: Transform

- type: entity
  id: TestRecyclerUser
  components:
  - type: Transform
";

    /// <summary>
    /// The component registers and carries the expected default tuning.
    /// </summary>
    [Test]
    public async Task ComponentRegisteredWithDefaults()
    {
        var server = Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var replacer = entityManager.SpawnEntity("TestRecyclerReplacer", mapData.GridCoords);

            Assert.That(entityManager.HasComponent<LightReplacerRecyclerComponent>(replacer), Is.True);

            var recycler = entityManager.GetComponent<LightReplacerRecyclerComponent>(replacer);
            Assert.Multiple(() =>
            {
                Assert.That(recycler.RecyclePoints, Is.EqualTo(0));
                Assert.That(recycler.PointsPerRecycle, Is.EqualTo(LightReplacerRecyclerConstants.DefaultPointsPerRecycle));
                Assert.That(recycler.PrintCost, Is.EqualTo(LightReplacerRecyclerConstants.DefaultPrintCost));
                Assert.That(recycler.MaxStoredBulbs, Is.EqualTo(LightReplacerRecyclerConstants.DefaultMaxStoredBulbs));
                Assert.That(recycler.PrintablePrototypes.Select(p => p.Id), Does.Contain("LightBulb"));
            });
        });
    }

    /// <summary>
    /// Feeding a glass shard via interaction grants recycle points and consumes the interaction.
    /// </summary>
    [Test]
    public async Task GlassShardRecycleAddsPoints()
    {
        var server = Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var replacer = entityManager.SpawnEntity("TestRecyclerReplacer", mapData.GridCoords);
            var user = entityManager.SpawnEntity("TestRecyclerUser", mapData.GridCoords);
            var shard = entityManager.SpawnEntity("TestRecyclerShard", mapData.GridCoords);
            var recycler = entityManager.GetComponent<LightReplacerRecyclerComponent>(replacer);
            var coords = entityManager.GetComponent<TransformComponent>(replacer).Coordinates;

            var args = new InteractUsingEvent(user, shard, replacer, coords);
            entityManager.EventBus.RaiseLocalEvent(replacer, args);

            Assert.Multiple(() =>
            {
                Assert.That(args.Handled, Is.True);
                Assert.That(recycler.RecyclePoints, Is.EqualTo(recycler.PointsPerRecycle));
            });
        });
    }

    /// <summary>
    /// A broken bulb routed to the recycler grants a point and reports the insert as handled.
    /// </summary>
    [Test]
    public async Task BrokenBulbInsertAddsPoints()
    {
        var server = Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var replacer = entityManager.SpawnEntity("TestRecyclerReplacer", mapData.GridCoords);
            var recycler = entityManager.GetComponent<LightReplacerRecyclerComponent>(replacer);

            var broken = entityManager.SpawnEntity("TestRecyclerBulbA", mapData.GridCoords);
            entityManager.GetComponent<LightBulbComponent>(broken).State = LightBulbState.Broken;

            var ev = new LightReplacerBrokenBulbInsertEvent(broken, null);
            entityManager.EventBus.RaiseLocalEvent(replacer, ref ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, Is.True);
                Assert.That(recycler.RecyclePoints, Is.EqualTo(recycler.PointsPerRecycle));
            });
        });
    }

    /// <summary>
    /// The storage cap rejects inserts once <see cref="LightReplacerRecyclerComponent.MaxStoredBulbs"/>
    /// bulbs are held.
    /// </summary>
    [Test]
    public async Task StorageCapRejectsOverfill()
    {
        var server = Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var replacer = entityManager.SpawnEntity("TestRecyclerReplacer", mapData.GridCoords);
            var recycler = entityManager.GetComponent<LightReplacerRecyclerComponent>(replacer);
            var storage = entityManager.GetComponent<LightReplacerComponent>(replacer).InsertedBulbs;

            for (var i = 0; i < recycler.MaxStoredBulbs; i++)
            {
                var bulb = entityManager.SpawnEntity("TestRecyclerBulbA", mapData.GridCoords);
                Assert.That(containerSystem.Insert(bulb, storage), Is.True, $"insert {i} should fit");
            }

            var overflow = entityManager.SpawnEntity("TestRecyclerBulbA", mapData.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(containerSystem.Insert(overflow, storage), Is.False, "insert past the cap should be rejected");
                Assert.That(storage.ContainedEntities, Has.Count.EqualTo(recycler.MaxStoredBulbs));
            });
        });
    }

    /// <summary>
    /// When storage holds a bulb whose prototype matches the broken one, that exact bulb is used in
    /// preference to a same-type bulb of a different prototype.
    /// </summary>
    [Test]
    public async Task ReplacePrefersExactPrototype()
    {
        var server = Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var poweredLight = entityManager.System<PoweredLightSystem>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var replacer = entityManager.SpawnEntity("TestRecyclerReplacer", mapData.GridCoords);
            var fixture = entityManager.SpawnEntity("TestRecyclerFixture", mapData.GridCoords);
            var storage = entityManager.GetComponent<LightReplacerComponent>(replacer).InsertedBulbs;

            var exact = entityManager.SpawnEntity("TestRecyclerBulbA", mapData.GridCoords);
            var other = entityManager.SpawnEntity("TestRecyclerBulbB", mapData.GridCoords);
            containerSystem.Insert(exact, storage);
            containerSystem.Insert(other, storage);

            // Stand-in for the broken fixture bulb; shares the prototype of the "exact" stored bulb.
            var broken = entityManager.SpawnEntity("TestRecyclerBulbA", mapData.GridCoords);

            var ev = new LightReplacerRecycleReplaceEvent(fixture, null, broken);
            entityManager.EventBus.RaiseLocalEvent(replacer, ref ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, Is.True);
                Assert.That(ev.Success, Is.True);
                Assert.That(poweredLight.GetBulb(fixture), Is.EqualTo(exact));
                Assert.That(storage.ContainedEntities, Is.EqualTo(new[] { other }));
            });
        });
    }

    /// <summary>
    /// With no exact prototype match, any stored bulb of the right type is used as a fallback.
    /// </summary>
    [Test]
    public async Task ReplaceFallsBackToSameType()
    {
        var server = Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var poweredLight = entityManager.System<PoweredLightSystem>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var replacer = entityManager.SpawnEntity("TestRecyclerReplacer", mapData.GridCoords);
            var fixture = entityManager.SpawnEntity("TestRecyclerFixture", mapData.GridCoords);
            var storage = entityManager.GetComponent<LightReplacerComponent>(replacer).InsertedBulbs;

            // Only a different-prototype, same-type bulb is available.
            var fallback = entityManager.SpawnEntity("TestRecyclerBulbB", mapData.GridCoords);
            containerSystem.Insert(fallback, storage);

            var broken = entityManager.SpawnEntity("TestRecyclerBulbA", mapData.GridCoords);

            var ev = new LightReplacerRecycleReplaceEvent(fixture, null, broken);
            entityManager.EventBus.RaiseLocalEvent(replacer, ref ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Success, Is.True);
                Assert.That(poweredLight.GetBulb(fixture), Is.EqualTo(fallback));
                Assert.That(storage.ContainedEntities, Is.Empty);
            });
        });
    }

    /// <summary>
    /// With empty storage but enough points, a bulb is printed into the fixture and the cost is spent.
    /// </summary>
    [Test]
    public async Task ReplacePrintsWhenStorageEmpty()
    {
        var server = Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var poweredLight = entityManager.System<PoweredLightSystem>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var replacer = entityManager.SpawnEntity("TestRecyclerReplacer", mapData.GridCoords);
            var fixture = entityManager.SpawnEntity("TestRecyclerFixture", mapData.GridCoords);
            var recycler = entityManager.GetComponent<LightReplacerRecyclerComponent>(replacer);

            // Earn exactly the print cost by recycling broken bulbs through the real point path.
            for (var i = 0; i < recycler.PrintCost; i++)
            {
                var scrap = entityManager.SpawnEntity("TestRecyclerBulbA", mapData.GridCoords);
                entityManager.GetComponent<LightBulbComponent>(scrap).State = LightBulbState.Broken;
                var insert = new LightReplacerBrokenBulbInsertEvent(scrap, null);
                entityManager.EventBus.RaiseLocalEvent(replacer, ref insert);
            }

            Assert.That(recycler.RecyclePoints, Is.EqualTo(recycler.PrintCost));

            // No broken bulb passed, so points only come from the stockpile above.
            var ev = new LightReplacerRecycleReplaceEvent(fixture, null, null);
            entityManager.EventBus.RaiseLocalEvent(replacer, ref ev);

            var printed = poweredLight.GetBulb(fixture);
            Assert.Multiple(() =>
            {
                Assert.That(ev.Success, Is.True);
                Assert.That(printed, Is.Not.Null);
                Assert.That(recycler.RecyclePoints, Is.EqualTo(0));
            });
            Assert.That(entityManager.GetComponent<MetaDataComponent>(printed!.Value).EntityPrototype?.ID, Is.EqualTo("LightBulb"));
        });
    }

    /// <summary>
    /// With empty storage and too few points, the replace fails and spends nothing.
    /// </summary>
    [Test]
    public async Task ReplaceFailsWhenNothingAvailable()
    {
        var server = Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var poweredLight = entityManager.System<PoweredLightSystem>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var replacer = entityManager.SpawnEntity("TestRecyclerReplacer", mapData.GridCoords);
            var fixture = entityManager.SpawnEntity("TestRecyclerFixture", mapData.GridCoords);
            var recycler = entityManager.GetComponent<LightReplacerRecyclerComponent>(replacer);
            var broken = entityManager.SpawnEntity("TestRecyclerBulbA", mapData.GridCoords);

            var ev = new LightReplacerRecycleReplaceEvent(fixture, null, broken);
            entityManager.EventBus.RaiseLocalEvent(replacer, ref ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, Is.True);
                Assert.That(ev.Success, Is.False);
                Assert.That(recycler.RecyclePoints, Is.EqualTo(0));
                Assert.That(poweredLight.GetBulb(fixture), Is.Null);
            });
        });
    }
}

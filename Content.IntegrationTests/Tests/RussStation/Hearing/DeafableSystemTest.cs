using Content.Shared.RussStation.Hearing;
using Content.Shared.RussStation.Hearing.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Hearing;

[TestFixture]
[TestOf(typeof(DeafableSystem))]
public sealed class DeafableSystemTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DeafTestEntity
  components:
  - type: Deafable

- type: entity
  id: DeafTestEntityWithTempDeafness
  components:
  - type: Deafable
  - type: TemporaryDeafness
";

    // ================================================================
    // DeafableSystem — CanHearAttemptEvent / IsDeaf state
    // ================================================================

    [Test]
    public async Task EntityWithoutDeafnessCanHearTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("DeafTestEntity", mapData.GridCoords);
            var deafable = entMan.GetComponent<DeafableComponent>(ent);

            Assert.That(deafable.IsDeaf, Is.False,
                "Entity with DeafableComponent but no deafness source should not be deaf");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TemporaryDeafnessMakesEntityDeafTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("DeafTestEntityWithTempDeafness", mapData.GridCoords);
            var deafable = entMan.GetComponent<DeafableComponent>(ent);

            Assert.That(deafable.IsDeaf, Is.True,
                "Entity with TemporaryDeafnessComponent should be deaf");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingTemporaryDeafnessRestoresHearingTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("DeafTestEntityWithTempDeafness", mapData.GridCoords);
            var deafable = entMan.GetComponent<DeafableComponent>(ent);

            Assert.That(deafable.IsDeaf, Is.True, "Precondition: entity should be deaf");

            entMan.RemoveComponent<TemporaryDeafnessComponent>(ent);

            Assert.That(deafable.IsDeaf, Is.False,
                "Removing TemporaryDeafnessComponent should restore hearing");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AddingTemporaryDeafnessDynamicallyMakesDeafTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("DeafTestEntity", mapData.GridCoords);
            var deafable = entMan.GetComponent<DeafableComponent>(ent);

            Assert.That(deafable.IsDeaf, Is.False, "Precondition: entity should hear");

            entMan.AddComponent<TemporaryDeafnessComponent>(ent);

            Assert.That(deafable.IsDeaf, Is.True,
                "Dynamically adding TemporaryDeafnessComponent should make entity deaf");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CanHearAttemptEventCancelledByDeafnessTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("DeafTestEntityWithTempDeafness", mapData.GridCoords);

            var ev = new CanHearAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(ent, ev);

            Assert.That(ev.Deaf, Is.True,
                "CanHearAttemptEvent should be cancelled (Deaf=true) for a deaf entity");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CanHearAttemptEventNotCancelledForHearingEntityTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("DeafTestEntity", mapData.GridCoords);

            var ev = new CanHearAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(ent, ev);

            Assert.That(ev.Deaf, Is.False,
                "CanHearAttemptEvent should not be cancelled for a hearing entity");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RejuvenateUpdatesDeafnessStateTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var deafSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DeafableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("DeafTestEntityWithTempDeafness", mapData.GridCoords);
            var deafable = entMan.GetComponent<DeafableComponent>(ent);

            Assert.That(deafable.IsDeaf, Is.True, "Precondition: entity should be deaf");

            // Remove the deafness source, then call UpdateIsDeaf (simulates what Rejuvenate triggers)
            entMan.RemoveComponent<TemporaryDeafnessComponent>(ent);
            deafSys.UpdateIsDeaf(ent);

            Assert.That(deafable.IsDeaf, Is.False,
                "UpdateIsDeaf should detect deafness source is gone");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IsDeafStateTracksDeafnessSourcesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("DeafTestEntity", mapData.GridCoords);
            var deafable = entMan.GetComponent<DeafableComponent>(ent);

            Assert.That(deafable.IsDeaf, Is.False, "Should start hearing");

            // Add deafness source
            entMan.AddComponent<TemporaryDeafnessComponent>(ent);
            Assert.That(deafable.IsDeaf, Is.True, "Should be deaf after adding source");

            // Remove deafness source
            entMan.RemoveComponent<TemporaryDeafnessComponent>(ent);
            Assert.That(deafable.IsDeaf, Is.False, "Should hear again after removing source");

            // Add again to verify re-application
            entMan.AddComponent<TemporaryDeafnessComponent>(ent);
            Assert.That(deafable.IsDeaf, Is.True, "Should be deaf again after re-adding source");
        });

        await pair.CleanReturnAsync();
    }
}

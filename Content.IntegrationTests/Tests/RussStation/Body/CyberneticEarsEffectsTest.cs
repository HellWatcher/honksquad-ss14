using System.Linq;
using Content.Server.RussStation.Body;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.RussStation.Body;
using Content.Shared.RussStation.Hearing;
using Content.Shared.RussStation.Hearing.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(CyberneticOrganEffectsSystem))]
public sealed class CyberneticEarsEffectsTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CyberEarsTestBody
  components:
  - type: Body

- type: entity
  id: CyberEarsTestEars
  components:
  - type: Organ
    category: Ears
  - type: CyberneticEars

- type: entity
  id: CyberEarsTestBodyWithEars
  components:
  - type: Body
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberEarsTestEars

- type: entity
  id: CyberEarsTestDeafBodyWithEars
  components:
  - type: Body
  - type: Deafable
  - type: TemporaryDeafness
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberEarsTestEars

- type: entity
  id: CyberEarsTestDeafBody
  components:
  - type: Body
  - type: Deafable
  - type: TemporaryDeafness
";

    [Test]
    public async Task EarsResistDeafnessTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberEarsTestBodyWithEars", mapData.GridCoords);

            var ev = new CanHearAttemptEvent();
            ev.Cancel();

            entMan.EventBus.RaiseLocalEvent(body, ev);

            Assert.That(ev.Cancelled, Is.False,
                "Cybernetic ears should uncancel deafness");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EarsNoResistanceWithoutOrganTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberEarsTestBody", mapData.GridCoords);

            var ev = new CanHearAttemptEvent();
            ev.Cancel();

            entMan.EventBus.RaiseLocalEvent(body, ev);

            Assert.That(ev.Cancelled, Is.True,
                "Deafness should persist without cybernetic ears");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EarsOverrideTemporaryDeafnessTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // Entity has DeafableComponent + TemporaryDeafness + CyberneticEars
            var body = entMan.SpawnEntity("CyberEarsTestDeafBodyWithEars", mapData.GridCoords);

            var ev = new CanHearAttemptEvent();
            ev.Cancel(); // Simulate something trying to deafen
            entMan.EventBus.RaiseLocalEvent(body, ev);

            Assert.That(ev.Deaf, Is.False,
                "Cybernetic ears should override temporary deafness (uncancel the event)");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeafBodyWithoutEarsRemainsDeafTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // Entity has DeafableComponent + TemporaryDeafness but NO ears
            var body = entMan.SpawnEntity("CyberEarsTestDeafBody", mapData.GridCoords);

            var ev = new CanHearAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(body, ev);

            Assert.That(ev.Deaf, Is.True,
                "Deaf entity without cybernetic ears should remain deaf");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EarsRemovalRestoresDeafnessVulnerabilityTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberEarsTestDeafBodyWithEars", mapData.GridCoords);

            // Verify ears override deafness
            var evBefore = new CanHearAttemptEvent();
            evBefore.Cancel();
            entMan.EventBus.RaiseLocalEvent(body, evBefore);
            Assert.That(evBefore.Cancelled, Is.False, "Precondition: ears should uncancel deafness");

            // Remove the ears organ
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            foreach (var ent in organContainer.ContainedEntities.ToList())
            {
                if (entMan.HasComponent<CyberneticEarsComponent>(ent))
                    containerSys.Remove(ent, organContainer);
            }

            var evAfter = new CanHearAttemptEvent();
            evAfter.Cancel();
            entMan.EventBus.RaiseLocalEvent(body, evAfter);
            Assert.That(evAfter.Cancelled, Is.True,
                "Deafness should persist after removing cybernetic ears");
        });

        await pair.CleanReturnAsync();
    }
}

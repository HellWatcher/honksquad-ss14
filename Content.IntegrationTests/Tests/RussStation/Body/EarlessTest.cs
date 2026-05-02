using System.Linq;
using Content.Server.RussStation.Body;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.RussStation.Hearing;
using Content.Shared.RussStation.Hearing.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(OrganLossSystem))]
[TestOf(typeof(DeafableSystem))]
public sealed class EarlessTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: EarlessTestEars
  components:
  - type: Organ
    category: Ears

- type: entity
  id: EarlessTestBodyWithEars
  components:
  - type: Body
  - type: Deafable
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: EarlessTestEars
";

    [Test]
    public async Task EarsPresentLeavesPatientHearingTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var deafableSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DeafableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EarlessTestBodyWithEars", mapData.GridCoords);

            // Force a recompute -- nothing else has triggered one yet, IsDeaf default is false.
            deafableSys.UpdateIsDeaf(body);

            var deafable = entMan.GetComponent<DeafableComponent>(body);
            Assert.That(deafable.IsDeaf, Is.False,
                "Body with at least one ears organ should not be deaf");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EarRemovalDeafensPatientTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EarlessTestBodyWithEars", mapData.GridCoords);
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            var ears = organContainer.ContainedEntities
                .First(o => entMan.TryGetComponent<OrganComponent>(o, out var c) && c.Category == "Ears");

            containerSys.Remove(ears, organContainer);

            var deafable = entMan.GetComponent<DeafableComponent>(body);
            Assert.That(deafable.IsDeaf, Is.True,
                "Body with no ears should be deaf");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EarReinsertionRestoresHearingTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EarlessTestBodyWithEars", mapData.GridCoords);
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            var ears = organContainer.ContainedEntities
                .First(o => entMan.TryGetComponent<OrganComponent>(o, out var c) && c.Category == "Ears");

            containerSys.Remove(ears, organContainer);
            Assert.That(entMan.GetComponent<DeafableComponent>(body).IsDeaf, Is.True,
                "Precondition: deaf without ears");

            containerSys.Insert(ears, organContainer, force: true);

            Assert.That(entMan.GetComponent<DeafableComponent>(body).IsDeaf, Is.False,
                "Reinserting ears should restore hearing");
        });

        await pair.CleanReturnAsync();
    }
}

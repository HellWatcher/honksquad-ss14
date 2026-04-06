using System.Linq;
using Content.Server.RussStation.Body;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.RussStation.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(CyberneticOrganEffectsSystem))]
public sealed class CyberneticLiverEffectsTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CyberLiverTestBody
  components:
  - type: Body

- type: entity
  id: CyberLiverTestLiver
  components:
  - type: Organ
    category: Liver
  - type: CyberneticLiver

- type: entity
  id: CyberLiverTestBodyWithLiver
  components:
  - type: Body
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberLiverTestLiver
";

    [Test]
    public async Task LiverAppliesOverdoseResistanceOnInsertTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberLiverTestBodyWithLiver", mapData.GridCoords);

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.True,
                "Body should have OverdoseResistanceComponent after liver insertion");

            var resistance = entMan.GetComponent<OverdoseResistanceComponent>(body);
            Assert.That(resistance.ThresholdMultiplier, Is.EqualTo(1.5f),
                "Threshold multiplier should match liver's configured value");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LiverRemovesOverdoseResistanceOnRemovalTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containerSys = entMan.System<SharedContainerSystem>();
            var body = entMan.SpawnEntity("CyberLiverTestBodyWithLiver", mapData.GridCoords);

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.True,
                "Precondition: resistance should be present");

            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            foreach (var ent in organContainer.ContainedEntities.ToList())
            {
                if (entMan.HasComponent<CyberneticLiverComponent>(ent))
                    containerSys.Remove(ent, organContainer);
            }

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.False,
                "OverdoseResistanceComponent should be removed with liver");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LiverNoResistanceWithoutOrganTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberLiverTestBody", mapData.GridCoords);

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.False,
                "Body without cybernetic liver should not have overdose resistance");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DynamicOrganInsertionTriggersEffectTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberLiverTestBody", mapData.GridCoords);

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.False);

            entMan.SpawnInContainerOrDrop("CyberLiverTestLiver", body, BodyComponent.ContainerID);

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.True,
                "Dynamic organ insertion should trigger liver effect");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LiverOverdoseThresholdMultiplierMatchesComponentTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberLiverTestBodyWithLiver", mapData.GridCoords);
            var resistance = entMan.GetComponent<OverdoseResistanceComponent>(body);

            Assert.That(resistance.ThresholdMultiplier, Is.EqualTo(1.5f),
                "OverdoseResistanceComponent multiplier should match CyberneticLiverComponent default (1.5)");
        });

        await pair.CleanReturnAsync();
    }
}

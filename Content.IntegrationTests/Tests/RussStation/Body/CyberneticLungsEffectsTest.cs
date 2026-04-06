using Content.Server.Body.Systems;
using Content.Server.RussStation.Body;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.RussStation.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(CyberneticOrganEffectsSystem))]
public sealed class CyberneticLungsEffectsTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CyberLungsTestBody
  components:
  - type: Body

- type: entity
  id: CyberLungsTestLungs
  components:
  - type: Organ
    category: Lungs
  - type: CyberneticLungs
  - type: Metabolizer
    metabolizerTypes:
    - Human
    stages: [Respiration]

- type: entity
  id: CyberLungsTestBodyWithLungs
  components:
  - type: Body
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: SolutionContainerManager
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberLungsTestLungs
";

    [Test]
    public async Task LungsFilterToxicGasesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberLungsTestBodyWithLungs", mapData.GridCoords);

            var containerSys = entMan.System<SharedContainerSystem>();
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            EntityUid lungEntity = default;
            foreach (var ent in organContainer.ContainedEntities)
            {
                if (entMan.HasComponent<CyberneticLungsComponent>(ent))
                {
                    lungEntity = ent;
                    break;
                }
            }

            Assert.That(lungEntity, Is.Not.EqualTo(default(EntityUid)),
                "Should find lungs in body");

            var lungs = entMan.GetComponent<CyberneticLungsComponent>(lungEntity);

            // Oxygen should be in oxygenating gases for Human metabolizer type
            Assert.That(lungs.OxygenatingGases, Is.Not.Empty,
                "Oxygenating gases should be resolved on insertion");
            Assert.That(lungs.OxygenatingGases.Contains(Gas.Oxygen), Is.True,
                "Oxygen should be an oxygenating gas for humans");

            // Create a gas mixture with oxygen + plasma (toxic)
            var gasMix = new GasMixture(10f);
            gasMix.SetMoles(Gas.Oxygen, 10f);
            gasMix.SetMoles(Gas.Plasma, 10f);

            var inhaledEvent = new InhaledGasEvent(gasMix);
            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            var relayedEvent = new BodyRelayedEvent<InhaledGasEvent>(
                new Entity<BodyComponent>(body, bodyComp), inhaledEvent);
            entMan.EventBus.RaiseLocalEvent(lungEntity, ref relayedEvent);

            // Oxygen should be unchanged (oxygenating gas, not filtered)
            Assert.That(gasMix.GetMoles(Gas.Oxygen), Is.EqualTo(10f).Within(0.01f),
                "Oxygenating gas should not be filtered");

            // Plasma should be halved (50% filter fraction)
            Assert.That(gasMix.GetMoles(Gas.Plasma), Is.EqualTo(5f).Within(0.01f),
                "Toxic gas should be filtered by 50%");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LungsClearOxygenatingGasesOnRemovalTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containerSys = entMan.System<SharedContainerSystem>();
            var body = entMan.SpawnEntity("CyberLungsTestBodyWithLungs", mapData.GridCoords);

            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            EntityUid lungEntity = default;
            foreach (var ent in organContainer.ContainedEntities)
            {
                if (entMan.HasComponent<CyberneticLungsComponent>(ent))
                {
                    lungEntity = ent;
                    break;
                }
            }

            var lungs = entMan.GetComponent<CyberneticLungsComponent>(lungEntity);
            Assert.That(lungs.OxygenatingGases, Is.Not.Empty,
                "Should have oxygenating gases while inserted");

            containerSys.Remove(lungEntity, organContainer);

            Assert.That(lungs.OxygenatingGases, Is.Empty,
                "Oxygenating gases should be cleared on removal");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LungsPreserveOxygenatingGasesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberLungsTestBodyWithLungs", mapData.GridCoords);

            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            EntityUid lungOrganEnt = default;
            foreach (var organ in organContainer.ContainedEntities)
            {
                if (entMan.HasComponent<CyberneticLungsComponent>(organ))
                {
                    lungOrganEnt = organ;
                    break;
                }
            }

            Assert.That(lungOrganEnt, Is.Not.EqualTo(default(EntityUid)), "Precondition: lungs organ should exist");
            var lungs = entMan.GetComponent<CyberneticLungsComponent>(lungOrganEnt);

            // Oxygen should be in the oxygenating set for a human metabolizer
            Assert.That(lungs.OxygenatingGases.Contains(Gas.Oxygen), Is.True,
                "Oxygen should be in the oxygenating gases set for a human-type body");

            // Create a gas mix and raise InhaledGasEvent via relay
            var gasMix = new GasMixture(1f);
            gasMix.SetMoles(Gas.Oxygen, 10f);
            gasMix.SetMoles(Gas.Plasma, 10f);

            var inhaleEvent = new InhaledGasEvent(gasMix);
            var relayEvent = new BodyRelayedEvent<InhaledGasEvent>(
                new Entity<BodyComponent>(body, entMan.GetComponent<BodyComponent>(body)),
                inhaleEvent);

            entMan.EventBus.RaiseLocalEvent(lungOrganEnt, ref relayEvent);

            // Oxygen (oxygenating) should be untouched
            Assert.That(gasMix.GetMoles(Gas.Oxygen), Is.EqualTo(10f),
                "Oxygenating gas (Oxygen) should not be filtered");

            // Plasma (toxic) should be reduced by FilterFraction (50%)
            Assert.That(gasMix.GetMoles(Gas.Plasma), Is.EqualTo(5f).Within(0.01f),
                "Toxic gas (Plasma) should be filtered by 50%");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LungsDynamicInsertionResolvesOxygenatingGasesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberLungsTestBody", mapData.GridCoords);

            // Dynamically insert lungs
            var lungEntity = entMan.SpawnInContainerOrDrop("CyberLungsTestLungs", body, BodyComponent.ContainerID);

            var lungs = entMan.GetComponent<CyberneticLungsComponent>(lungEntity);
            Assert.That(lungs.OxygenatingGases, Is.Not.Empty,
                "Dynamically inserted lungs should resolve oxygenating gases");
            Assert.That(lungs.OxygenatingGases.Contains(Gas.Oxygen), Is.True,
                "Oxygen should be in oxygenating gases after dynamic insertion");
        });

        await pair.CleanReturnAsync();
    }
}

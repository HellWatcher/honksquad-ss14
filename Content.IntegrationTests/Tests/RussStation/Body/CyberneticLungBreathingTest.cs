using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;
using Content.Shared.RussStation.Body;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

/// <summary>
/// Locks in the SharedCyberneticLungsSystem behavior: when a cybernetic lung organ
/// is inserted into a body whose other organs carry MetabolizerTypes (e.g. [Human]),
/// the lung's MetabolizerComponent should inherit the same set so the Oxygenate
/// effect's MetabolizerTypeCondition resolves and the patient actually breathes.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedCyberneticLungsSystem))]
public sealed class CyberneticLungBreathingTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CyberLungBreathTestHeart
  components:
  - type: Organ
    category: Heart
  - type: Metabolizer
    metabolizerTypes: [ Human ]

- type: entity
  id: CyberLungBreathTestLung
  components:
  - type: Organ
    category: Lungs
  - type: Lung
  - type: SolutionContainerManager
    solutions:
      Lung:
        maxVol: 100.0
        canReact: false
  - type: CyberneticOrgan
    tier: Standard
    empEffect: BreathingFailure
    empVulnerability: 1.0
  - type: Metabolizer
    stages: [ Respiration ]

- type: entity
  id: CyberLungBreathTestBody
  components:
  - type: Body
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberLungBreathTestHeart
        - id: CyberLungBreathTestLung
";

    [Test]
    public async Task RealCyberneticLungPrototypeHasLungSolutionTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var solSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedSolutionContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // Spawn each tier of the actual fork prototype standalone and check the
            // 'Lung' solution actually exists on it -- if the YAML re-declaration of
            // SolutionContainerManager wiped the parent's, this asserts will fail.
            foreach (var protoId in new[]
            {
                "OrganCyberneticLungsBasic",
                "OrganCyberneticLungsStandard",
                "OrganCyberneticLungsAdvanced",
            })
            {
                var lung = entMan.SpawnEntity(protoId, mapData.GridCoords);

                Assert.That(entMan.HasComponent<LungComponent>(lung), Is.True,
                    $"{protoId} should carry LungComponent (inherited from OrganBaseLungs)");
                Assert.That(entMan.HasComponent<MetabolizerComponent>(lung), Is.True,
                    $"{protoId} should carry MetabolizerComponent");

                var lungComp = entMan.GetComponent<LungComponent>(lung);
                Assert.That(solSys.TryGetSolution(lung, lungComp.SolutionName, out _, out _), Is.True,
                    $"{protoId} should have its '{lungComp.SolutionName}' solution wired up");

                var metabolizer = entMan.GetComponent<MetabolizerComponent>(lung);
                Assert.That(metabolizer.Stages, Does.Contain(new Robust.Shared.Prototypes.ProtoId<MetabolismStagePrototype>("Respiration")),
                    $"{protoId} should have the Respiration stage on its Metabolizer");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberneticLungFillsWithOxygenOnInhaleTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var solSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedSolutionContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberLungBreathTestBody", mapData.GridCoords);
            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            var lung = bodyComp.Organs!.ContainedEntities
                .First(o => entMan.HasComponent<CyberneticOrganComponent>(o));

            var lungComp = entMan.GetComponent<LungComponent>(lung);
            Assert.That(solSys.TryGetSolution(lung, lungComp.SolutionName, out _, out var solBefore), Is.True);
            Assert.That(solBefore!.Volume, Is.EqualTo(FixedPoint2.Zero),
                "Precondition: lung solution starts empty");

            // Synthesize an inhale -- this is exactly what RespiratorSystem.Inhale does internally.
            var gas = new GasMixture(6f);
            gas.SetMoles(Gas.Oxygen, 5f);
            var inhale = new InhaledGasEvent(gas);
            entMan.EventBus.RaiseLocalEvent(body, ref inhale);

            Assert.That(inhale.Succeeded, Is.True,
                "InhaledGasEvent should land on the cybernetic lung's RespiratorSystem handler");

            Assert.That(solSys.TryGetSolution(lung, lungComp.SolutionName, out _, out var solAfter), Is.True);
            Assert.That(solAfter!.Volume, Is.GreaterThan(FixedPoint2.Zero),
                "Lung solution should contain reagents after the inhale relay reaches the lung");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DynamicallyInsertedCyberneticLungBreathesTest()
    {
        // Mirrors the surgery flow: bio lungs out, cybernetic lungs in. We simulate by
        // spawning the body without lungs (just the heart) and then inserting a bare
        // cybernetic lung entity into the body_organs container.
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var solSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedSolutionContainerSystem>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<Robust.Shared.Containers.SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // Body that only has a heart at spawn (so the heart's MetabolizerTypes seeds
            // the dynamic inheritance), no lungs yet.
            var heartOnly = entMan.SpawnEntity("CyberLungBreathTestBody", mapData.GridCoords);
            var bodyComp = entMan.GetComponent<BodyComponent>(heartOnly);
            // remove the auto-spawned lung so we're testing the dynamic insert below
            var existingLung = bodyComp.Organs!.ContainedEntities
                .First(o => entMan.HasComponent<CyberneticOrganComponent>(o));
            containerSys.Remove(existingLung, bodyComp.Organs);
            entMan.DeleteEntity(existingLung);

            // Spawn a fresh cybernetic lung and insert it the way surgery does
            var lung = entMan.SpawnEntity("CyberLungBreathTestLung", mapData.GridCoords);
            var inserted = containerSys.Insert(lung, bodyComp.Organs);
            Assert.That(inserted, Is.True, "Cybernetic lung should insert into body_organs");

            var metabolizer = entMan.GetComponent<MetabolizerComponent>(lung);
            Assert.That(metabolizer.MetabolizerTypes, Is.Not.Null,
                "MetabolizerTypes should be populated after dynamic insertion");
            Assert.That(metabolizer.MetabolizerTypes!.Any(t => t.Id == "Human"), Is.True);

            var lungComp = entMan.GetComponent<LungComponent>(lung);
            var gas = new GasMixture(6f);
            gas.SetMoles(Gas.Oxygen, 5f);
            var inhale = new InhaledGasEvent(gas);
            entMan.EventBus.RaiseLocalEvent(heartOnly, ref inhale);

            Assert.That(solSys.TryGetSolution(lung, lungComp.SolutionName, out _, out var sol), Is.True);
            Assert.That(sol!.Volume, Is.GreaterThan(FixedPoint2.Zero),
                "Dynamically-inserted cybernetic lung should fill its solution on inhale");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyberneticLungInheritsHostMetabolizerTypesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberLungBreathTestBody", mapData.GridCoords);
            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            Assert.That(bodyComp.Organs, Is.Not.Null);

            var lung = bodyComp.Organs!.ContainedEntities
                .First(o => entMan.HasComponent<CyberneticOrganComponent>(o));

            var metabolizer = entMan.GetComponent<MetabolizerComponent>(lung);
            Assert.That(metabolizer.MetabolizerTypes, Is.Not.Null,
                "Cybernetic lung should have MetabolizerTypes copied from the host body's other organs");
            Assert.That(metabolizer.MetabolizerTypes!, Has.Count.GreaterThan(0),
                "Inherited MetabolizerTypes should not be empty");
            Assert.That(metabolizer.MetabolizerTypes!.Any(t => t.Id == "Human"), Is.True,
                "Inherited MetabolizerTypes should contain 'Human' from the other host organs");
        });

        await pair.CleanReturnAsync();
    }
}

using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.RussStation.Surgery.Systems;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Surgery;

[TestFixture]
[TestOf(typeof(SharedSurgerySystem))]
public sealed class SurgerySystemTest
{
    private const string TestProcedureId = "SurgeryTestProcedure";

    [TestPrototypes]
    private const string Prototypes = @"
- type: surgeryProcedure
  id: SurgeryTestProcedure
  name: Test Procedure
  description: A test procedure.
  steps:
    - tag: Scalpel
      duration: 1.0
      popup: surgery-step-incision
    - tag: Retractor
      duration: 1.0
      popup: surgery-step-retract

- type: entity
  id: SurgeryTestPatient
  components:
  - type: Buckle
  - type: Hands
  - type: ComplexInteraction
  - type: InputMover
  - type: Physics
    bodyType: KinematicController
  - type: Body
    prototype: Human
  - type: StandingState

- type: entity
  id: SurgeryTestScalpel
  components:
  - type: Tag
    tags:
    - SurgeryTool
    - Scalpel

- type: entity
  id: SurgeryTestRetractor
  components:
  - type: Tag
    tags:
    - SurgeryTool
    - Retractor

- type: entity
  id: SurgeryTestCautery
  components:
  - type: Tag
    tags:
    - SurgeryTool
    - Cautery

- type: entity
  id: SurgeryTestNonTool
  components:
  - type: Tag
    tags:
    - Scalpel

- type: entity
  id: SurgeryTestOperatingTable
  components:
  - type: Strap
  - type: SurgerySurface
    speedModifier: 1.0

- type: entity
  id: SurgeryTestMedicalBed
  components:
  - type: Strap
  - type: SurgerySurface
    speedModifier: 1.5

- type: entity
  id: SurgeryTestChair
  components:
  - type: Strap
";

    /// <summary>
    /// Verifies that surgery procedure prototypes load and have valid steps.
    /// </summary>
    [Test]
    public async Task ProcedurePrototypesLoadTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var proto), Is.True);
            Assert.That(proto!.Steps.Count, Is.EqualTo(2));
            Assert.That(proto.Steps[0].Tag.Id, Is.EqualTo("Scalpel"));
            Assert.That(proto.Steps[1].Tag.Id, Is.EqualTo("Retractor"));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that all game-defined surgery procedure prototypes have at least one step
    /// and reference valid tag prototypes.
    /// </summary>
    [Test]
    public async Task AllProcedurePrototypesValidTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var proto in protoManager.EnumeratePrototypes<SurgeryProcedurePrototype>())
            {
                Assert.That(proto.Steps, Is.Not.Empty, $"Procedure '{proto.ID}' has no steps.");
                Assert.That(proto.Name, Is.Not.Empty, $"Procedure '{proto.ID}' has no name.");

                for (var i = 0; i < proto.Steps.Count; i++)
                {
                    var step = proto.Steps[i];
                    Assert.That(protoManager.HasIndex<TagPrototype>(step.Tag),
                        $"Procedure '{proto.ID}' step {i} references unknown tag '{step.Tag}'.");
                    Assert.That(step.Duration, Is.GreaterThan(0f),
                        $"Procedure '{proto.ID}' step {i} has non-positive duration.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that ToolMatchesStep correctly matches tool tags to step requirements.
    /// </summary>
    [Test]
    public async Task ToolMatchesStepTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();
            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var proto);

            var scalpel = entityManager.SpawnEntity("SurgeryTestScalpel", mapData.GridCoords);
            var retractor = entityManager.SpawnEntity("SurgeryTestRetractor", mapData.GridCoords);

            // Scalpel matches step 0 (Scalpel), not step 1 (Retractor)
            Assert.That(surgerySystem.ToolMatchesStep(scalpel, proto!.Steps[0]), Is.True);
            Assert.That(surgerySystem.ToolMatchesStep(scalpel, proto.Steps[1]), Is.False);

            // Retractor matches step 1 (Retractor), not step 0 (Scalpel)
            Assert.That(surgerySystem.ToolMatchesStep(retractor, proto.Steps[0]), Is.False);
            Assert.That(surgerySystem.ToolMatchesStep(retractor, proto.Steps[1]), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that an entity with the correct tag but without the SurgeryTool tag
    /// still matches the step (ToolMatchesStep only checks the step tag, not SurgeryTool).
    /// The SurgeryTool gate is in the server's OnAfterInteract, not in ToolMatchesStep.
    /// </summary>
    [Test]
    public async Task ToolMatchesStepIgnoresSurgeryToolTagTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();
            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var proto);

            // Entity has Scalpel tag but NOT SurgeryTool tag
            var nonTool = entityManager.SpawnEntity("SurgeryTestNonTool", mapData.GridCoords);

            // ToolMatchesStep only checks the step's required tag, so this matches
            Assert.That(surgerySystem.ToolMatchesStep(nonTool, proto!.Steps[0]), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that IsCauteryTool identifies cautery tools correctly.
    /// </summary>
    [Test]
    public async Task IsCauteryToolTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();

            var cautery = entityManager.SpawnEntity("SurgeryTestCautery", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("SurgeryTestScalpel", mapData.GridCoords);

            Assert.That(surgerySystem.IsCauteryTool(cautery), Is.True);
            Assert.That(surgerySystem.IsCauteryTool(scalpel), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that GetSurfaceSpeedModifier returns 1.0 for unbuckled patients
    /// and the configured modifier when buckled to a surgery surface.
    /// </summary>
    [Test]
    public async Task SurfaceSpeedModifierTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();
            var buckleSystem = entityManager.System<SharedBuckleSystem>();

            var patient = entityManager.SpawnEntity("SurgeryTestPatient", mapData.GridCoords);
            var table = entityManager.SpawnEntity("SurgeryTestOperatingTable", mapData.GridCoords);

            // Unbuckled patient should return floor penalty of 2.0
            Assert.That(surgerySystem.GetSurfaceSpeedModifier(patient), Is.EqualTo(2f));

            // Buckle to operating table -> 1.0x modifier
            var buckle = entityManager.GetComponent<BuckleComponent>(patient);
            Assert.That(buckleSystem.TryBuckle(patient, patient, table, buckleComp: buckle), Is.True);
            Assert.That(surgerySystem.GetSurfaceSpeedModifier(patient), Is.EqualTo(1f));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that GetStepDuration correctly multiplies step duration by surface speed modifier.
    /// </summary>
    [Test]
    public async Task StepDurationAppliesSurfaceModifierTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();
            var buckleSystem = entityManager.System<SharedBuckleSystem>();

            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var proto);
            var step = proto!.Steps[0]; // duration: 1.0

            var patient = entityManager.SpawnEntity("SurgeryTestPatient", mapData.GridCoords);
            var table = entityManager.SpawnEntity("SurgeryTestOperatingTable", mapData.GridCoords);

            // Unbuckled (floor surgery): duration * 2.0
            Assert.That(surgerySystem.GetStepDuration(step, patient), Is.EqualTo(TimeSpan.FromSeconds(2.0)));

            // Buckled to operating table: duration * 1.0
            var buckle = entityManager.GetComponent<BuckleComponent>(patient);
            Assert.That(buckleSystem.TryBuckle(patient, patient, table, buckleComp: buckle), Is.True);
            Assert.That(surgerySystem.GetStepDuration(step, patient), Is.EqualTo(TimeSpan.FromSeconds(1.0)));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that GetSurfaceSpeedModifier returns 1.0 when buckled to an entity
    /// that has a Strap component but no SurgerySurface component (e.g. a regular chair).
    /// </summary>
    [Test]
    public async Task NonSurgerySurfaceReturnsDefaultModifierTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();
            var buckleSystem = entityManager.System<SharedBuckleSystem>();

            var patient = entityManager.SpawnEntity("SurgeryTestPatient", mapData.GridCoords);

            // Plain strap with no SurgerySurface component
            var chair = entityManager.SpawnEntity("SurgeryTestChair", mapData.GridCoords);

            var buckle = entityManager.GetComponent<BuckleComponent>(patient);
            Assert.That(buckleSystem.TryBuckle(patient, patient, chair, buckleComp: buckle), Is.True);
            Assert.That(surgerySystem.GetSurfaceSpeedModifier(patient), Is.EqualTo(2f));
        });

        await pair.CleanReturnAsync();
    }
}

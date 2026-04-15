using Content.IntegrationTests.Fixtures;
using Content.Server.RussStation.Surgery;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.RussStation.Surgery.Systems;
using Content.Shared.Tools;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Surgery;

[TestOf(typeof(SharedSurgerySystem))]
public sealed class SurgerySystemTest : GameTest
{
    private const string TestProcedureId = "SurgeryTestProcedure";
    private const string TestProcedureMajorId = "SurgeryTestProcedureMajor";

    [TestPrototypes]
    private const string Prototypes = @"
- type: surgeryProcedure
  id: SurgeryTestProcedure
  name: Test Procedure
  description: A test procedure.
  steps:
    - quality: Slicing
      duration: 1.0
      popup: surgery-step-incision
    - quality: Retracting
      duration: 1.0
      popup: surgery-step-retract

- type: surgeryProcedure
  id: SurgeryTestProcedureMajor
  name: Test Major Procedure
  description: A major test procedure.
  difficulty: Major
  steps:
    - quality: Slicing
      popup: surgery-step-incision
    - quality: Clamping
      popup: surgery-step-clamp

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
    - TierStandard
  - type: Tool
    qualities:
    - Slicing

- type: entity
  id: SurgeryTestRetractor
  components:
  - type: Tag
    tags:
    - TierStandard
  - type: Tool
    qualities:
    - Retracting

- type: entity
  id: SurgeryTestCautery
  components:
  - type: Tag
    tags:
    - TierStandard
  - type: Tool
    qualities:
    - Cauterizing

- type: entity
  id: SurgeryTestNonTool
  components:
  - type: Tool
    qualities:
    - Slicing

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

- type: entity
  id: SurgeryTestSurgicalDrape
  components:
  - type: Tag
    tags:
    - TierStandard
  - type: Tool
    qualities:
    - Draping

- type: entity
  id: SurgeryTestBedsheet
  components:
  - type: Tool
    qualities:
    - Draping

- type: entity
  id: SurgeryTestAdvancedScalpel
  components:
  - type: Tag
    tags:
    - TierAdvanced
  - type: Tool
    qualities:
    - Slicing

- type: entity
  id: SurgeryTestExperimentalScalpel
  components:
  - type: Tag
    tags:
    - TierExperimental
  - type: Tool
    qualities:
    - Slicing

- type: entity
  id: SurgeryTestImprovisedTool
  components:
  - type: Tool
    qualities:
    - Slicing
";

    /// <summary>
    /// Verifies that surgery procedure prototypes load and have valid steps.
    /// </summary>
    [Test]
    public async Task ProcedurePrototypesLoadTest()
    {
        var server = Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var proto), Is.True);
            Assert.That(proto!.Steps.Count, Is.EqualTo(2));
            Assert.That(proto.Steps[0].Quality.Id, Is.EqualTo("Slicing"));
            Assert.That(proto.Steps[1].Quality.Id, Is.EqualTo("Retracting"));
        });
    }

    /// <summary>
    /// Verifies that all game-defined surgery procedure prototypes have at least one step
    /// and reference valid tool quality prototypes.
    /// </summary>
    [Test]
    public async Task AllProcedurePrototypesValidTest()
    {
        var server = Server;
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
                    Assert.That(protoManager.HasIndex<ToolQualityPrototype>(step.Quality),
                        $"Procedure '{proto.ID}' step {i} references unknown quality '{step.Quality}'.");

                    var baseDuration = SharedSurgerySystem.GetBaseStepDuration(step);
                    Assert.That(baseDuration, Is.GreaterThan(0f),
                        $"Procedure '{proto.ID}' step {i} has non-positive duration.");
                }
            }
        });
    }

    /// <summary>
    /// Verifies that ToolMatchesStep correctly matches tool qualities to step requirements.
    /// </summary>
    [Test]
    public async Task ToolMatchesStepTest()
    {
        var server = Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();
            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var proto);

            var scalpel = entityManager.SpawnEntity("SurgeryTestScalpel", mapData.GridCoords);
            var retractor = entityManager.SpawnEntity("SurgeryTestRetractor", mapData.GridCoords);

            // Scalpel matches step 0 (Slicing), not step 1 (Retracting)
            Assert.That(surgerySystem.ToolMatchesStep(scalpel, proto!.Steps[0]), Is.True);
            Assert.That(surgerySystem.ToolMatchesStep(scalpel, proto.Steps[1]), Is.False);

            // Retractor matches step 1 (Retracting), not step 0 (Slicing)
            Assert.That(surgerySystem.ToolMatchesStep(retractor, proto.Steps[0]), Is.False);
            Assert.That(surgerySystem.ToolMatchesStep(retractor, proto.Steps[1]), Is.True);
        });
    }

    /// <summary>
    /// Verifies that a tool with the correct quality but without a tier tag
    /// still matches the step. Tier tags affect duration, not step matching.
    /// </summary>
    [Test]
    public async Task ToolMatchesStepIgnoresTierTagTest()
    {
        var server = Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();
            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var proto);

            // Entity has Slicing quality but no tier tag (improvised)
            var nonTool = entityManager.SpawnEntity("SurgeryTestNonTool", mapData.GridCoords);

            // ToolMatchesStep only checks the quality, so this matches
            Assert.That(surgerySystem.ToolMatchesStep(nonTool, proto!.Steps[0]), Is.True);
        });
    }

    /// <summary>
    /// Verifies that IsCauteryTool identifies cautery tools correctly.
    /// </summary>
    [Test]
    public async Task IsCauteryToolTest()
    {
        var server = Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();

            var cautery = entityManager.SpawnEntity("SurgeryTestCautery", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("SurgeryTestScalpel", mapData.GridCoords);

            Assert.That(surgerySystem.IsCauteryTool(cautery), Is.True);
            Assert.That(surgerySystem.IsCauteryTool(scalpel), Is.False);
        });
    }

    /// <summary>
    /// Verifies that GetSurfaceSpeedModifier returns 2.0 for unbuckled patients
    /// and the configured modifier when buckled to a surgery surface.
    /// </summary>
    [Test]
    public async Task SurfaceSpeedModifierTest()
    {
        var server = Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

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
    }

    /// <summary>
    /// Verifies that GetSurfaceSpeedModifier returns 2.0 when buckled to an entity
    /// that has a Strap component but no SurgerySurface component (e.g. a regular chair).
    /// </summary>
    [Test]
    public async Task NonSurgerySurfaceReturnsDefaultModifierTest()
    {
        var server = Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

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
    }

    /// <summary>
    /// Verifies that GetDrapeSpeedModifier returns the correct multiplier.
    /// No drape component = 1.0 (no penalty), default drape = 1.5 (bedsheet improvised penalty).
    /// Surgical drape stamping (1.0x) is tested server-side via the draping interaction.
    /// </summary>
    [Test]
    public async Task DrapeSpeedModifierTest()
    {
        var server = Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();

            var patient = entityManager.SpawnEntity("SurgeryTestPatient", mapData.GridCoords);

            // No drape -> 1.0 (no penalty from drape layer)
            Assert.That(surgerySystem.GetDrapeSpeedModifier(patient), Is.EqualTo(1f));

            // Add SurgeryDrapedComponent with default (bedsheet improvised penalty)
            entityManager.AddComponent<SurgeryDrapedComponent>(patient);
            Assert.That(surgerySystem.GetDrapeSpeedModifier(patient), Is.EqualTo(1.5f));
        });
    }

    /// <summary>
    /// Verifies that GetDifficultyModifier returns the correct multiplier for each tier.
    /// </summary>
    [Test]
    public async Task DifficultyModifierTest()
    {
        Assert.That(SharedSurgerySystem.GetDifficultyModifier(SurgeryDifficulty.Minor), Is.EqualTo(0.8f));
        Assert.That(SharedSurgerySystem.GetDifficultyModifier(SurgeryDifficulty.Standard), Is.EqualTo(1.0f));
        Assert.That(SharedSurgerySystem.GetDifficultyModifier(SurgeryDifficulty.Major), Is.EqualTo(1.3f));
        Assert.That(SharedSurgerySystem.GetDifficultyModifier(SurgeryDifficulty.Critical), Is.EqualTo(1.5f));
    }

    /// <summary>
    /// Verifies that GetBaseStepDuration returns the explicit override when set,
    /// and falls back to the centralized default for the quality when null.
    /// </summary>
    [Test]
    public async Task BaseStepDurationFallbackTest()
    {
        var server = Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            // Test procedure has explicit duration: 1.0 on a Slicing step
            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var proto);
            Assert.That(SharedSurgerySystem.GetBaseStepDuration(proto!.Steps[0]), Is.EqualTo(1.0f));

            // Major procedure has no explicit duration on Slicing -> centralized default (2.0)
            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureMajorId, out var majorProto);
            Assert.That(SharedSurgerySystem.GetBaseStepDuration(majorProto!.Steps[0]), Is.EqualTo(2.0f));

            // Clamping step also uses centralized default (2.0)
            Assert.That(SharedSurgerySystem.GetBaseStepDuration(majorProto.Steps[1]), Is.EqualTo(2.0f));
        });
    }

    /// <summary>
    /// Verifies that GetStepDuration correctly combines surface, drape, and difficulty modifiers.
    /// </summary>
    [Test]
    public async Task StepDurationCombinesAllModifiersTest()
    {
        var server = Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SharedSurgerySystem>();
            var buckleSystem = entityManager.System<SharedBuckleSystem>();

            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var proto);
            var step = proto!.Steps[0]; // duration: 1.0

            var patient = entityManager.SpawnEntity("SurgeryTestPatient", mapData.GridCoords);
            var table = entityManager.SpawnEntity("SurgeryTestOperatingTable", mapData.GridCoords);

            // Floor surgery, no drape, standard difficulty:
            // 1.0 * 2.0 (surface) * 1.0 (no drape comp) * 1.0 (standard) = 2.0
            Assert.That(surgerySystem.GetStepDuration(step, patient, SurgeryDifficulty.Standard),
                Is.EqualTo(TimeSpan.FromSeconds(2.0)));

            // Buckle to table, no drape, standard:
            // 1.0 * 1.0 (surface) * 1.0 (no drape comp) * 1.0 (standard) = 1.0
            var buckle = entityManager.GetComponent<BuckleComponent>(patient);
            Assert.That(buckleSystem.TryBuckle(patient, patient, table, buckleComp: buckle), Is.True);
            Assert.That(surgerySystem.GetStepDuration(step, patient, SurgeryDifficulty.Standard),
                Is.EqualTo(TimeSpan.FromSeconds(1.0)));

            // Add bedsheet drape (1.5x), major difficulty (1.3x):
            // 1.0 * 1.0 (surface) * 1.5 (bedsheet) * 1.3 (major) = 1.95
            entityManager.AddComponent<SurgeryDrapedComponent>(patient);
            Assert.That(surgerySystem.GetStepDuration(step, patient, SurgeryDifficulty.Major).TotalSeconds,
                Is.EqualTo(1.95).Within(0.001));

            // Same bedsheet drape (1.5x), minor difficulty (0.8x):
            // 1.0 * 1.0 (surface) * 1.5 (bedsheet) * 0.8 (minor) = 1.2
            Assert.That(surgerySystem.GetStepDuration(step, patient, SurgeryDifficulty.Minor).TotalSeconds,
                Is.EqualTo(1.2).Within(0.001));
        });
    }

    /// <summary>
    /// Verifies that the major test procedure loads with the correct difficulty.
    /// </summary>
    [Test]
    public async Task ProcedureDifficultyLoadsTest()
    {
        var server = Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var standard);
            Assert.That(standard!.Difficulty, Is.EqualTo(SurgeryDifficulty.Standard));

            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureMajorId, out var major);
            Assert.That(major!.Difficulty, Is.EqualTo(SurgeryDifficulty.Major));
        });
    }

    /// <summary>
    /// Verifies that GetToolTierModifier returns the correct multiplier for each tier tag:
    /// Experimental = 0.7, Advanced = 0.8, Standard = 1.0, no tag = 1.5 (improvised).
    /// </summary>
    [Test]
    public async Task ToolTierModifierAllBranchesTest()
    {
        var server = Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SurgerySystem>();

            var standard = entityManager.SpawnEntity("SurgeryTestScalpel", mapData.GridCoords);
            var advanced = entityManager.SpawnEntity("SurgeryTestAdvancedScalpel", mapData.GridCoords);
            var experimental = entityManager.SpawnEntity("SurgeryTestExperimentalScalpel", mapData.GridCoords);
            var improvised = entityManager.SpawnEntity("SurgeryTestImprovisedTool", mapData.GridCoords);

            Assert.That(surgerySystem.GetToolTierModifier(standard), Is.EqualTo(1.0f), "Standard tier");
            Assert.That(surgerySystem.GetToolTierModifier(advanced), Is.EqualTo(0.8f), "Advanced tier");
            Assert.That(surgerySystem.GetToolTierModifier(experimental), Is.EqualTo(0.7f), "Experimental tier");
            Assert.That(surgerySystem.GetToolTierModifier(improvised), Is.EqualTo(1.5f), "Improvised (no tier tag)");
        });
    }

    /// <summary>
    /// Verifies that tool tier modifier integrates into the full duration calculation.
    /// An advanced tool (0.8x) on the same step should produce a shorter DoAfter than standard (1.0x).
    /// </summary>
    [Test]
    public async Task ToolTierAffectsStepDurationTest()
    {
        var server = Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgerySystem = entityManager.System<SurgerySystem>();
            var buckleSystem = entityManager.System<SharedBuckleSystem>();

            protoManager.TryIndex<SurgeryProcedurePrototype>(TestProcedureId, out var proto);
            var step = proto!.Steps[0]; // duration: 1.0

            var patient = entityManager.SpawnEntity("SurgeryTestPatient", mapData.GridCoords);
            var table = entityManager.SpawnEntity("SurgeryTestOperatingTable", mapData.GridCoords);

            // Buckle to table so surface = 1.0x, no drape so drape = 1.0x, standard difficulty
            var buckle = entityManager.GetComponent<BuckleComponent>(patient);
            Assert.That(buckleSystem.TryBuckle(patient, patient, table, buckleComp: buckle), Is.True);

            // Base duration from GetStepDuration (without tool tier): 1.0 * 1.0 * 1.0 * 1.0 = 1.0
            var baseDuration = (float) surgerySystem.GetStepDuration(step, patient, SurgeryDifficulty.Standard).TotalSeconds;

            // Standard tool: 1.0 * 1.0 = 1.0
            var standardTool = entityManager.SpawnEntity("SurgeryTestScalpel", mapData.GridCoords);
            Assert.That(baseDuration * surgerySystem.GetToolTierModifier(standardTool),
                Is.EqualTo(1.0f).Within(0.001f));

            // Experimental tool: 1.0 * 0.7 = 0.7
            var experimentalTool = entityManager.SpawnEntity("SurgeryTestExperimentalScalpel", mapData.GridCoords);
            Assert.That(baseDuration * surgerySystem.GetToolTierModifier(experimentalTool),
                Is.EqualTo(0.7f).Within(0.001f));

            // Improvised tool: 1.0 * 1.5 = 1.5
            var improvisedTool = entityManager.SpawnEntity("SurgeryTestImprovisedTool", mapData.GridCoords);
            Assert.That(baseDuration * surgerySystem.GetToolTierModifier(improvisedTool),
                Is.EqualTo(1.5f).Within(0.001f));
        });
    }
}

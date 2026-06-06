using Content.IntegrationTests.Fixtures;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Systems;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Surgery;

/// <summary>
/// Tests for <see cref="SharedSurgerySystem"/>. The procedure prototype and step-preset
/// validation scenarios live here; tool matching/tier scenarios live in
/// <c>SurgerySystemTest.Tools.cs</c>, and surface/drape/difficulty duration scenarios live
/// in <c>SurgerySystemTest.Modifiers.cs</c>. All partials share the <see cref="Prototypes"/>
/// fixture below.
/// </summary>
[TestOf(typeof(SharedSurgerySystem))]
public sealed partial class SurgerySystemTest : GameTest
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
            Assert.That(proto.Steps[0].GetQuality().Id, Is.EqualTo("Slicing"));
            Assert.That(proto.Steps[1].GetQuality().Id, Is.EqualTo("Retracting"));
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
                    var quality = step.GetQuality();
                    Assert.That(protoManager.HasIndex<ToolQualityPrototype>(quality),
                        $"Procedure '{proto.ID}' step {i} references unknown quality '{quality}'.");

                    var baseDuration = SharedSurgerySystem.GetBaseStepDuration(step);
                    Assert.That(baseDuration, Is.GreaterThan(0f),
                        $"Procedure '{proto.ID}' step {i} has non-positive duration.");
                }
            }
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
    /// For every shipped procedure, verifies that each step whose preset is non-None resolves to
    /// the same tool quality and duration the preset table declares, unless the step explicitly
    /// overrides them. Guards against a procedure picking a preset and then silently diverging.
    /// </summary>
    [Test]
    public async Task ProcedurePresetsResolveConsistentlyTest()
    {
        var server = Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var proto in protoManager.EnumeratePrototypes<SurgeryProcedurePrototype>())
            {
                for (var i = 0; i < proto.Steps.Count; i++)
                {
                    var step = proto.Steps[i];
                    if (step.Preset == SurgeryStepPreset.None)
                        continue;

                    var defaults = SurgeryStepPresets.Resolve(step.Preset);

                    if (step.Quality == null)
                    {
                        Assert.That(step.GetQuality(), Is.EqualTo(defaults.Quality),
                            $"Procedure '{proto.ID}' step {i} ({step.Preset}) inherits the wrong tool quality.");
                    }

                    if (!step.Duration.HasValue && defaults.Duration.HasValue)
                    {
                        Assert.That(step.GetDuration(), Is.EqualTo(defaults.Duration),
                            $"Procedure '{proto.ID}' step {i} ({step.Preset}) inherits the wrong duration.");
                    }

                    if (!step.Repeatable && defaults.Repeatable)
                    {
                        Assert.That(step.GetRepeatable(), Is.True,
                            $"Procedure '{proto.ID}' step {i} ({step.Preset}) should be repeatable via preset.");
                    }
                }
            }
        });
    }
}

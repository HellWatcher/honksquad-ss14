using Content.Server.RussStation.Surgery;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Surgery;

/// <summary>
/// Tool-matching and tool-tier scenarios for the surgery system: which tools satisfy a
/// step, cautery detection, and how the tool tier tag scales step duration. Shares the
/// <c>Prototypes</c> fixture declared in <c>SurgerySystemTest.cs</c>.
/// </summary>
public sealed partial class SurgerySystemTest
{
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

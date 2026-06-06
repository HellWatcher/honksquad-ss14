using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.RussStation.Surgery.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Surgery;

/// <summary>
/// Step-duration modifier scenarios for the surgery system: surface speed, drape penalty,
/// difficulty scaling, and the combined duration calculation. Shares the <c>Prototypes</c>
/// fixture declared in <c>SurgerySystemTest.cs</c>.
/// </summary>
public sealed partial class SurgerySystemTest
{
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
}

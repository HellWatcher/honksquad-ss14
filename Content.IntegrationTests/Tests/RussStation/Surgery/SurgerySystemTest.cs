using Content.IntegrationTests.Fixtures;
using Content.Server.RussStation.Surgery;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.RussStation.Surgery.Systems;
using Content.Shared.Standing;
using Content.Shared.Tools;
using Robust.Shared.GameObjects;
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

# A surgeon mob with a real hand slot, so surgery DoAfters (NeedHand) can run.
- type: entity
  id: SurgeryTestSurgeon
  components:
  - type: DoAfter
  - type: Hands
    hands:
      hand_right:
        location: Right
    sortedHands:
    - hand_right
  - type: ComplexInteraction
  - type: InputMover
  - type: Physics
    bodyType: KinematicController

# A patient that can take and store damage, for testing healing steps.
- type: entity
  id: SurgeryTestHealPatient
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
  - type: Damageable
  # Upstream moved damageContainer off Damageable onto the new Injurable component, and
  # DamageableSystem only applies DamageDealtEvent to entities with Injurable. Without it
  # the patient can be neither damaged nor healed.
  - type: Injurable
    damageContainer: Biological

# Single repeatable healing step: a flat budget that auto-repeats until damage is drained.
- type: surgeryProcedure
  id: SurgeryTestTendProcedure
  name: Test Tend Procedure
  description: A repeatable healing procedure.
  steps:
    - quality: Retracting
      duration: 1.0
      repeatable: true
      popup: surgery-step-retract
      healing:
        types:
          Blunt: 1
      healingFlat: 6
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

    /// <summary>
    /// Drives a whole procedure end to end: a draping selection starts the surgery, each tool step
    /// advances it, and a cautery closes the patient and tears the surgery state down. Exercises the
    /// menu validator, the step handler, and the cautery-close path together.
    /// </summary>
    [Test]
    public async Task FullSurgeryFlowTest()
    {
        var server = Server;
        var sEntMan = server.EntMan;
        var mapData = await Pair.CreateTestMap();

        EntityUid surgeon = default;
        EntityUid patient = default;
        EntityUid scalpel = default;
        EntityUid retractor = default;
        EntityUid cautery = default;
        NetEntity netPatient = default;
        NetEntity netDrape = default;

        await server.WaitPost(() =>
        {
            var standing = sEntMan.System<StandingStateSystem>();

            surgeon = sEntMan.SpawnEntity("SurgeryTestSurgeon", mapData.GridCoords);
            patient = sEntMan.SpawnEntity("SurgeryTestPatient", mapData.GridCoords);
            scalpel = sEntMan.SpawnEntity("SurgeryTestScalpel", mapData.GridCoords);
            retractor = sEntMan.SpawnEntity("SurgeryTestRetractor", mapData.GridCoords);
            cautery = sEntMan.SpawnEntity("SurgeryTestCautery", mapData.GridCoords);
            var drape = sEntMan.SpawnEntity("SurgeryTestSurgicalDrape", mapData.GridCoords);

            // The session must drive the surgeon so the server sees them as the procedure sender.
            server.PlayerMan.SetAttachedEntity(ServerSession, surgeon, force: true);
            standing.Down(patient, force: true);

            netPatient = sEntMan.GetNetEntity(patient);
            netDrape = sEntMan.GetNetEntity(drape);
        });

        await Pair.RunTicksSync(5);

        // Select the procedure: drapes the patient and starts the surgery.
        await Client.WaitPost(() =>
            Client.EntMan.EntityNetManager.SendSystemNetworkMessage(
                new SelectSurgeryProcedureEvent(netPatient, netDrape, TestProcedureId)));

        await RunUntil(() =>
            sEntMan.HasComponent<ActiveSurgeryComponent>(patient) &&
            sEntMan.HasComponent<SurgeryDrapedComponent>(patient));

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.TryGetComponent<ActiveSurgeryComponent>(patient, out var active), Is.True);
            Assert.That(active!.CurrentStep, Is.EqualTo(0));
        });

        // Step 0 (Slicing) with the scalpel.
        await UseToolOnPatient(sEntMan, surgeon, scalpel, patient);
        await RunUntil(() => GetStep(sEntMan, patient) >= 1);

        // Step 1 (Retracting) with the retractor: the final step.
        await UseToolOnPatient(sEntMan, surgeon, retractor, patient);
        await RunUntil(() => GetStep(sEntMan, patient) >= 2);

        // Cautery universal close: removes the surgery and the drape.
        await UseToolOnPatient(sEntMan, surgeon, cautery, patient);
        await RunUntil(() => !sEntMan.HasComponent<ActiveSurgeryComponent>(patient));

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.HasComponent<ActiveSurgeryComponent>(patient), Is.False, "Surgery should be closed.");
            Assert.That(sEntMan.HasComponent<SurgeryDrapedComponent>(patient), Is.False, "Drape should be removed.");
        });
    }

    /// <summary>
    /// The menu validator must refuse a selection when the patient is still standing: no drape,
    /// no active surgery.
    /// </summary>
    [Test]
    public async Task ProcedureSelectionRejectedWhenStandingTest()
    {
        var server = Server;
        var sEntMan = server.EntMan;
        var mapData = await Pair.CreateTestMap();

        EntityUid patient = default;
        NetEntity netPatient = default;
        NetEntity netDrape = default;

        await server.WaitPost(() =>
        {
            var surgeon = sEntMan.SpawnEntity("SurgeryTestSurgeon", mapData.GridCoords);
            patient = sEntMan.SpawnEntity("SurgeryTestPatient", mapData.GridCoords);
            var drape = sEntMan.SpawnEntity("SurgeryTestSurgicalDrape", mapData.GridCoords);

            server.PlayerMan.SetAttachedEntity(ServerSession, surgeon, force: true);
            // Patient is deliberately left standing.

            netPatient = sEntMan.GetNetEntity(patient);
            netDrape = sEntMan.GetNetEntity(drape);
        });

        await Pair.RunTicksSync(5);

        await Client.WaitPost(() =>
            Client.EntMan.EntityNetManager.SendSystemNetworkMessage(
                new SelectSurgeryProcedureEvent(netPatient, netDrape, TestProcedureId)));

        await Pair.RunTicksSync(15);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.HasComponent<ActiveSurgeryComponent>(patient), Is.False,
                "A standing patient must not be draped into surgery.");
            Assert.That(sEntMan.HasComponent<SurgeryDrapedComponent>(patient), Is.False);
        });
    }

    /// <summary>
    /// A repeatable healing step auto-repeats until the patient's matching damage is drained.
    /// Exercises the step handler's repeat branch and the healing-budget application together.
    /// </summary>
    [Test]
    public async Task RepeatableHealStepDrainsDamageTest()
    {
        var server = Server;
        var sEntMan = server.EntMan;
        var mapData = await Pair.CreateTestMap();

        EntityUid surgeon = default;
        EntityUid patient = default;
        EntityUid retractor = default;
        NetEntity netPatient = default;
        NetEntity netDrape = default;

        await server.WaitPost(() =>
        {
            var standing = sEntMan.System<StandingStateSystem>();
            var damageable = sEntMan.System<DamageableSystem>();

            surgeon = sEntMan.SpawnEntity("SurgeryTestSurgeon", mapData.GridCoords);
            patient = sEntMan.SpawnEntity("SurgeryTestHealPatient", mapData.GridCoords);
            retractor = sEntMan.SpawnEntity("SurgeryTestRetractor", mapData.GridCoords);
            var drape = sEntMan.SpawnEntity("SurgeryTestSurgicalDrape", mapData.GridCoords);

            server.PlayerMan.SetAttachedEntity(ServerSession, surgeon, force: true);
            standing.Down(patient, force: true);

            // Give the patient more Blunt damage than one heal budget (6) can clear, forcing a repeat.
            var blunt = new DamageSpecifier();
            blunt.DamageDict["Blunt"] = FixedPoint2.New(10);
            damageable.TryChangeDamage(patient, blunt, ignoreResistances: true);

            netPatient = sEntMan.GetNetEntity(patient);
            netDrape = sEntMan.GetNetEntity(drape);
        });

        await Pair.RunTicksSync(5);

        await Client.WaitPost(() =>
            Client.EntMan.EntityNetManager.SendSystemNetworkMessage(
                new SelectSurgeryProcedureEvent(netPatient, netDrape, "SurgeryTestTendProcedure")));

        await RunUntil(() => sEntMan.HasComponent<ActiveSurgeryComponent>(patient));

        // One retractor use kicks off the repeatable tend step; it auto-repeats to drain the damage.
        await UseToolOnPatient(sEntMan, surgeon, retractor, patient);
        await RunUntil(() => GetBlunt(sEntMan, patient) <= 0f);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetBlunt(sEntMan, patient), Is.EqualTo(0f).Within(0.01f),
                "The repeatable tend step should drain Blunt damage to zero.");
        });
    }

    private static int GetStep(IEntityManager entMan, EntityUid patient)
    {
        return entMan.TryGetComponent<ActiveSurgeryComponent>(patient, out var active) ? active.CurrentStep : -1;
    }

    private static float GetBlunt(IEntityManager entMan, EntityUid patient)
    {
        if (!entMan.TryGetComponent<DamageableComponent>(patient, out var dmg))
            return -1f;

        // Read damage through the system accessor; DamageableComponent.Damage is access-restricted.
        var positive = entMan.System<DamageableSystem>().GetPositiveDamage((patient, dmg));
        return positive.DamageDict.TryGetValue("Blunt", out var value) ? (float) value : 0f;
    }

    /// <summary>
    /// Simulates the surgeon using a tool on the patient by raising the interaction the surgery
    /// system listens for. <c>canReach</c> is forced so the handler runs without spatial setup.
    /// </summary>
    private async Task UseToolOnPatient(IEntityManager entMan, EntityUid surgeon, EntityUid tool, EntityUid patient)
    {
        await Server.WaitPost(() =>
        {
            var coords = entMan.GetComponent<TransformComponent>(patient).Coordinates;
            var ev = new AfterInteractUsingEvent(surgeon, tool, patient, coords, canReach: true);
            entMan.EventBus.RaiseLocalEvent(patient, ev, false);
        });
    }

    /// <summary>
    /// Runs ticks in small batches until the server-evaluated <paramref name="condition"/> holds,
    /// failing the test if it never does within the tick budget. Robust to the server tick rate.
    /// </summary>
    private async Task RunUntil(Func<bool> condition, int maxTicks = 800, int chunk = 5)
    {
        var elapsed = 0;
        while (elapsed <= maxTicks)
        {
            var done = false;
            await Server.WaitPost(() => done = condition());
            if (done)
                return;

            await Pair.RunTicksSync(chunk);
            elapsed += chunk;
        }

        Assert.Fail($"Condition not satisfied within {maxTicks} ticks.");
    }
}

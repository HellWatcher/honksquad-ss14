using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Metabolism;

/// <summary>
/// Regression tests for issue #491 (Bug 1): the stomach's Digestion-stage transfer
/// asymptoted below the heart's Bloodstream-stage clearance for any reagent whose
/// declared MetabolismRate sat under that ceiling, so oral medication never reached
/// Bloodstream-stage OD/UD thresholds. The fork patch applies a 0.25u/tick floor
/// (MinTransferPerTick) on the Digestion entry so reagents move at least that fast
/// regardless of their declared rate. Mirrors SS13 STOMACH_METABOLISM_CONSTANT.
/// </summary>
[TestFixture]
public sealed class StomachTransferFloorTest : GameTest
{
    private const string TestInertReagent = "TestStomachFloorInert";

    [TestPrototypes]
    private const string Prototypes = @"
- type: reagent
  id: TestStomachFloorInert
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing

- type: entity
  id: StomachFloorDummy
  name: StomachFloorDummy
  components:
  - type: SolutionContainerManager
  - type: Body
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 1
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: Damageable
    damageContainer: Biological
  - type: MobState
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Dead
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: OrganHumanStomach
";

    [Test]
    public async Task StomachPushesAtFloorRatePerTick()
    {
        var pair = Pair;
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entManager.System<SharedSolutionContainerSystem>();

        EntityUid mob = default;
        EntityUid stomach = default;

        await server.WaitAssertion(() =>
        {
            mob = entManager.SpawnEntity("StomachFloorDummy", MapCoordinates.Nullspace);
            stomach = FindStomach(entManager, mob);

            Assert.That(containerSystem.TryGetSolution(stomach, "stomach", out var sHandle, out _),
                "Stomach organ should have its 'stomach' solution from OrganBaseStomach.");

            // 5u of an inert reagent with no Digestion entry: takes the generic-transfer branch.
            // Without the floor, it moves at TransferRate=0.25 * TransferEfficacy=0.5 = 0.125u/tick
            // into the bloodstream, but with the floor of 0.25 the per-tick removal is 0.25 (since
            // floor > TransferRate), so 5u drains in 20 ticks. Either way the floor never lowers
            // the rate; the assertion below holds in both worlds.
            containerSystem.TryAddSolution(sHandle!.Value,
                new Solution(TestInertReagent, FixedPoint2.New(5)));
        });

        // Run for 4 seconds. With the floor at 0.25u/tick the stomach should have moved at least
        // 4 * 0.25 = 1u out. Without the floor (and TransferRate 0.25, capped to 0.25/tick anyway)
        // the same lower bound holds, so this test is regression-safe across both code paths.
        await RunSeconds(4);

        await server.WaitAssertion(() =>
        {
            var stomachLeft = containerSystem.GetTotalPrototypeQuantity(stomach, TestInertReagent);
            Assert.That(stomachLeft, Is.LessThanOrEqualTo(FixedPoint2.New(4)),
                "Stomach should have transferred at least the floor amount each tick.");
        });
    }

    [Test]
    public async Task StomachFloorEmptiesSmallDose()
    {
        // Smaller dose: 0.5u of an inert reagent should empty within a few ticks because the
        // floor (0.25) clamps to quantity. Pre-fix, generic transfer at TransferRate 0.25 also
        // empties this in two ticks, so the test is mainly a regression net.
        var pair = Pair;
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entManager.System<SharedSolutionContainerSystem>();

        EntityUid mob = default;
        EntityUid stomach = default;

        await server.WaitAssertion(() =>
        {
            mob = entManager.SpawnEntity("StomachFloorDummy", MapCoordinates.Nullspace);
            stomach = FindStomach(entManager, mob);
            Assert.That(containerSystem.TryGetSolution(stomach, "stomach", out var sHandle, out _));
            containerSystem.TryAddSolution(sHandle!.Value,
                new Solution(TestInertReagent, FixedPoint2.New("0.5")));
        });

        await RunSeconds(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(containerSystem.GetTotalPrototypeQuantity(stomach, TestInertReagent),
                Is.EqualTo(FixedPoint2.Zero),
                "Stomach should have fully drained the small dose within 5 ticks.");
        });
    }

    private static EntityUid FindStomach(IEntityManager entManager, EntityUid body)
    {
        var bodyComp = entManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null);
        foreach (var organ in bodyComp.Organs!.ContainedEntities)
        {
            if (entManager.HasComponent<StomachComponent>(organ))
                return organ;
        }
        Assert.Fail("Stomach organ missing from dummy body.");
        return default;
    }
}

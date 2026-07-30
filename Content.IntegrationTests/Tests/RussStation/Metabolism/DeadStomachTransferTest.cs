using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Metabolism;

/// <summary>
/// Regression tests for issue #491 (Bug 2): the stomach's Digestion-stage
/// generic transfer branch used to drain reagents into the bloodstream on
/// corpses, even for reagents flagged !WorksOnTheDead. The fork patch in
/// <c>MetabolizerSystem.cs</c> resolves isDead against the organ's body
/// (not the organ itself) and gates the generic branch on that.
/// </summary>
[TestFixture]
public sealed class DeadStomachTransferTest : GameTest
{
    private static readonly ProtoId<DamageGroupPrototype> ToxinGroup = "Toxin";

    private const string TestInertReagent = "TestDeadStomachInert";

    [TestPrototypes]
    private const string Prototypes = @"
- type: reagent
  id: TestDeadStomachInert
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing

- type: entity
  id: DeadStomachDummy
  name: DeadStomachDummy
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
  # Upstream moved damageContainer off Damageable onto Injurable; without it the
  # entity has no damage container and can be neither damaged nor healed.
  - type: Injurable
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
    public async Task LivingStomachStillTransfers()
    {
        var pair = Pair;
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entManager.System<SharedSolutionContainerSystem>();

        EntityUid mob = default;
        EntityUid stomach = default;

        await server.WaitAssertion(() =>
        {
            mob = entManager.SpawnEntity("DeadStomachDummy", MapCoordinates.Nullspace);
            stomach = FindStomach(entManager, mob);

            Assert.That(containerSystem.TryGetSolution(stomach, "stomach", out var sHandle, out _),
                "Stomach organ should have its 'stomach' solution from OrganBaseStomach.");

            containerSystem.TryAddSolution(sHandle!.Value,
                new Solution(TestInertReagent, FixedPoint2.New(20)));
        });

        await RunSeconds(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(containerSystem.GetTotalPrototypeQuantity(stomach, TestInertReagent),
                Is.LessThan(FixedPoint2.New(20)),
                "Alive mob: stomach should drain via generic transfer.");
            Assert.That(containerSystem.GetTotalPrototypeQuantity(mob, TestInertReagent),
                Is.GreaterThan(FixedPoint2.Zero),
                "Alive mob: bloodstream should receive the transferred reagent.");
        });
    }

    [Test]
    public async Task DeadStomachFreezesGenericTransfer()
    {
        var pair = Pair;
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var containerSystem = entManager.System<SharedSolutionContainerSystem>();
        var mobStateSystem = entManager.System<MobStateSystem>();
        var damSystem = entManager.System<DamageableSystem>();

        EntityUid mob = default;
        EntityUid stomach = default;

        await server.WaitAssertion(() =>
        {
            mob = entManager.SpawnEntity("DeadStomachDummy", MapCoordinates.Nullspace);
            stomach = FindStomach(entManager, mob);

            Assert.That(containerSystem.TryGetSolution(stomach, "stomach", out var sHandle, out _));

            var damage = new DamageSpecifier(protoMan.Index(ToxinGroup), FixedPoint2.New(10000));
            damSystem.TryChangeDamage(mob, damage, true);
            Assert.That(mobStateSystem.IsDead(mob), Is.True, "Dummy should be dead after lethal damage.");

            containerSystem.TryAddSolution(sHandle!.Value,
                new Solution(TestInertReagent, FixedPoint2.New(20)));
        });

        await RunSeconds(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(containerSystem.GetTotalPrototypeQuantity(stomach, TestInertReagent),
                Is.EqualTo(FixedPoint2.New(20)),
                "Dead mob: stomach must not drain reagents without WorksOnTheDead.");
            Assert.That(containerSystem.GetTotalPrototypeQuantity(mob, TestInertReagent),
                Is.EqualTo(FixedPoint2.Zero),
                "Dead mob: bloodstream must not receive reagents from the stomach.");
        });
    }

    private static EntityUid FindStomach(IEntityManager entManager, EntityUid body)
    {
        var bodyComp = entManager.GetComponent<BodyComponent>(body);
        Assert.That(bodyComp.Organs, Is.Not.Null, "Body container should be initialized.");
        foreach (var organ in bodyComp.Organs!.ContainedEntities)
        {
            if (entManager.HasComponent<StomachComponent>(organ))
                return organ;
        }
        Assert.Fail("Stomach organ missing from dummy body.");
        return default;
    }
}

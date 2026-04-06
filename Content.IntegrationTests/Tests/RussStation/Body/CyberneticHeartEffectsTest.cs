using Content.Server.RussStation.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(CyberneticOrganEffectsSystem))]
public sealed class CyberneticHeartEffectsTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CyberHeartTestBody
  components:
  - type: Body
  - type: MobState
    allowedStates:
    - Alive
    - Critical
    - Dead
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Critical
      200: Dead
  - type: Damageable
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: SolutionContainerManager
  - type: Hunger

- type: entity
  id: CyberHeartTestHeart
  components:
  - type: Organ
    category: Heart
  - type: CyberneticHeart

- type: entity
  id: CyberHeartTestBodyWithHeart
  components:
  - type: Body
  - type: MobState
    allowedStates:
    - Alive
    - Critical
    - Dead
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Critical
      200: Dead
  - type: Damageable
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: SolutionContainerManager
  - type: Hunger
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberHeartTestHeart
";

    [Test]
    public async Task HeartInjectsEpinephrineOnCritTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var mobStateSystem = entMan.System<MobStateSystem>();
            var solutionSys = entMan.System<SharedSolutionContainerSystem>();
            var body = entMan.SpawnEntity("CyberHeartTestBodyWithHeart", mapData.GridCoords);

            var bloodstream = entMan.GetComponent<BloodstreamComponent>(body);
            Assert.That(solutionSys.TryGetSolution(body, bloodstream.BloodSolutionName,
                out _, out var bloodBefore), Is.True);
            var epiBefore = bloodBefore.GetTotalPrototypeQuantity("Epinephrine");
            Assert.That(epiBefore, Is.EqualTo(FixedPoint2.Zero), "No epinephrine before crit");

            mobStateSystem.ChangeMobState(body, MobState.Critical);

            Assert.That(solutionSys.TryGetSolution(body, bloodstream.BloodSolutionName,
                out _, out var bloodAfter), Is.True);
            var epiAfter = bloodAfter.GetTotalPrototypeQuantity("Epinephrine");
            Assert.That(epiAfter, Is.GreaterThan(FixedPoint2.Zero),
                "Epinephrine should be injected on crit");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeartCooldownPreventsRepeatedInjectionTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var mobStateSystem = entMan.System<MobStateSystem>();
            var solutionSys = entMan.System<SharedSolutionContainerSystem>();
            var body = entMan.SpawnEntity("CyberHeartTestBodyWithHeart", mapData.GridCoords);
            var bloodstream = entMan.GetComponent<BloodstreamComponent>(body);

            // First crit
            mobStateSystem.ChangeMobState(body, MobState.Critical);
            solutionSys.TryGetSolution(body, bloodstream.BloodSolutionName, out _, out var blood1);
            var afterFirst = blood1.GetTotalPrototypeQuantity("Epinephrine");
            Assert.That(afterFirst, Is.GreaterThan(FixedPoint2.Zero), "First crit should inject");

            // Reset to alive, then crit again immediately
            mobStateSystem.ChangeMobState(body, MobState.Alive);
            mobStateSystem.ChangeMobState(body, MobState.Critical);
            solutionSys.TryGetSolution(body, bloodstream.BloodSolutionName, out _, out var blood2);
            var afterSecond = blood2.GetTotalPrototypeQuantity("Epinephrine");
            Assert.That(afterSecond, Is.EqualTo(afterFirst),
                "Second crit within cooldown should not inject again");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeartNoEffectWithoutOrganTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var mobStateSystem = entMan.System<MobStateSystem>();
            var solutionSys = entMan.System<SharedSolutionContainerSystem>();
            var body = entMan.SpawnEntity("CyberHeartTestBody", mapData.GridCoords);
            var bloodstream = entMan.GetComponent<BloodstreamComponent>(body);

            mobStateSystem.ChangeMobState(body, MobState.Critical);

            solutionSys.TryGetSolution(body, bloodstream.BloodSolutionName, out _, out var blood);
            var epi = blood.GetTotalPrototypeQuantity("Epinephrine");
            Assert.That(epi, Is.EqualTo(FixedPoint2.Zero),
                "No epinephrine without cybernetic heart");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeartDoesNotInjectOnNonCritStateChangeTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedSolutionContainerSystem>();
        var mobStateSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<MobStateSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberHeartTestBodyWithHeart", mapData.GridCoords);

            // Transition to Dead (not Critical) -- should not inject
            mobStateSys.ChangeMobState(body, MobState.Dead);

            var bloodstream = entMan.GetComponent<BloodstreamComponent>(body);
            Assert.That(containerSys.TryGetSolution(body, bloodstream.BloodSolutionName, out _, out var solution),
                Is.True);
            Assert.That(solution.GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.Zero),
                "Heart should not inject on state changes other than Critical");
        });

        await pair.CleanReturnAsync();
    }
}

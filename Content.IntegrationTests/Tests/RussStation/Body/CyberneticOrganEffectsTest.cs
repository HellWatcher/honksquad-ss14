using System.Linq;
using Content.Server.Body.Systems;
using Content.Server.RussStation.Body;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Flash;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.RussStation.Body;
using Content.Shared.RussStation.Hearing;
using Content.Shared.RussStation.Hearing.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(CyberneticOrganEffectsSystem))]
public sealed class CyberneticOrganEffectsTest
{
    [TestPrototypes]
    private const string Prototypes = @"
# Base body with no organs
- type: entity
  id: CyberTestBody
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

# Heart organ with CyberneticHeart marker
- type: entity
  id: CyberTestHeart
  components:
  - type: Organ
    category: Heart
  - type: CyberneticHeart

# Eyes organ with CyberneticEyes marker
- type: entity
  id: CyberTestEyes
  components:
  - type: Organ
    category: Eyes
  - type: CyberneticEyes

# Lungs organ with CyberneticLungs marker + Metabolizer for species detection
- type: entity
  id: CyberTestLungs
  components:
  - type: Organ
    category: Lungs
  - type: CyberneticLungs
  - type: Metabolizer
    metabolizerTypes:
    - Human
    stages: [Respiration]

# Stomach organ with CyberneticStomach marker
- type: entity
  id: CyberTestStomach
  components:
  - type: Organ
    category: Stomach
  - type: CyberneticStomach

# Ears organ with CyberneticEars marker
- type: entity
  id: CyberTestEars
  components:
  - type: Organ
    category: Ears
  - type: CyberneticEars

# Liver organ with CyberneticLiver marker
- type: entity
  id: CyberTestLiver
  components:
  - type: Organ
    category: Liver
  - type: CyberneticLiver

# Body pre-filled with heart
- type: entity
  id: CyberTestBodyWithHeart
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
        - id: CyberTestHeart

# Body pre-filled with eyes
- type: entity
  id: CyberTestBodyWithEyes
  components:
  - type: Body
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: SolutionContainerManager
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberTestEyes

# Body pre-filled with lungs
- type: entity
  id: CyberTestBodyWithLungs
  components:
  - type: Body
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: SolutionContainerManager
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberTestLungs

# Body pre-filled with stomach
- type: entity
  id: CyberTestBodyWithStomach
  components:
  - type: Body
  - type: Hunger
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberTestStomach

# Body pre-filled with ears
- type: entity
  id: CyberTestBodyWithEars
  components:
  - type: Body
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberTestEars

# Body pre-filled with liver
- type: entity
  id: CyberTestBodyWithLiver
  components:
  - type: Body
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberTestLiver

# Body with ears + deafable + temporary deafness (for override test)
- type: entity
  id: CyberTestDeafBodyWithEars
  components:
  - type: Body
  - type: Deafable
  - type: TemporaryDeafness
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberTestEars

# Body with deafable + temporary deafness but NO ears
- type: entity
  id: CyberTestDeafBody
  components:
  - type: Body
  - type: Deafable
  - type: TemporaryDeafness
";

    // ================================================================
    // Heart - epinephrine auto-injection on entering crit
    // ================================================================

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
            var body = entMan.SpawnEntity("CyberTestBodyWithHeart", mapData.GridCoords);

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
            var body = entMan.SpawnEntity("CyberTestBodyWithHeart", mapData.GridCoords);
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
            var body = entMan.SpawnEntity("CyberTestBody", mapData.GridCoords);
            var bloodstream = entMan.GetComponent<BloodstreamComponent>(body);

            mobStateSystem.ChangeMobState(body, MobState.Critical);

            solutionSys.TryGetSolution(body, bloodstream.BloodSolutionName, out _, out var blood);
            var epi = blood.GetTotalPrototypeQuantity("Epinephrine");
            Assert.That(epi, Is.EqualTo(FixedPoint2.Zero),
                "No epinephrine without cybernetic heart");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Eyes - flash protection
    // ================================================================

    [Test]
    public async Task EyesCancelFlashTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBodyWithEyes", mapData.GridCoords);

            var ev = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Cancelled, Is.True,
                "Flash attempt should be cancelled with cybernetic eyes");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EyesNoProtectionWithoutOrganTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBody", mapData.GridCoords);

            var ev = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Cancelled, Is.False,
                "Flash should not be cancelled without cybernetic eyes");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Lungs - toxic gas filtering
    // ================================================================

    [Test]
    public async Task LungsFilterToxicGasesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBodyWithLungs", mapData.GridCoords);

            var containerSys = entMan.System<SharedContainerSystem>();
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            EntityUid lungEntity = default;
            foreach (var ent in organContainer.ContainedEntities)
            {
                if (entMan.HasComponent<CyberneticLungsComponent>(ent))
                {
                    lungEntity = ent;
                    break;
                }
            }

            Assert.That(lungEntity, Is.Not.EqualTo(default(EntityUid)),
                "Should find lungs in body");

            var lungs = entMan.GetComponent<CyberneticLungsComponent>(lungEntity);

            // Oxygen should be in oxygenating gases for Human metabolizer type
            Assert.That(lungs.OxygenatingGases, Is.Not.Empty,
                "Oxygenating gases should be resolved on insertion");
            Assert.That(lungs.OxygenatingGases.Contains(Gas.Oxygen), Is.True,
                "Oxygen should be an oxygenating gas for humans");

            // Create a gas mixture with oxygen + plasma (toxic)
            var gasMix = new GasMixture(10f);
            gasMix.SetMoles(Gas.Oxygen, 10f);
            gasMix.SetMoles(Gas.Plasma, 10f);

            var inhaledEvent = new InhaledGasEvent(gasMix);
            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            var relayedEvent = new BodyRelayedEvent<InhaledGasEvent>(
                new Entity<BodyComponent>(body, bodyComp), inhaledEvent);
            entMan.EventBus.RaiseLocalEvent(lungEntity, ref relayedEvent);

            // Oxygen should be unchanged (oxygenating gas, not filtered)
            Assert.That(gasMix.GetMoles(Gas.Oxygen), Is.EqualTo(10f).Within(0.01f),
                "Oxygenating gas should not be filtered");

            // Plasma should be halved (50% filter fraction)
            Assert.That(gasMix.GetMoles(Gas.Plasma), Is.EqualTo(5f).Within(0.01f),
                "Toxic gas should be filtered by 50%");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LungsClearOxygenatingGasesOnRemovalTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containerSys = entMan.System<SharedContainerSystem>();
            var body = entMan.SpawnEntity("CyberTestBodyWithLungs", mapData.GridCoords);

            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            EntityUid lungEntity = default;
            foreach (var ent in organContainer.ContainedEntities)
            {
                if (entMan.HasComponent<CyberneticLungsComponent>(ent))
                {
                    lungEntity = ent;
                    break;
                }
            }

            var lungs = entMan.GetComponent<CyberneticLungsComponent>(lungEntity);
            Assert.That(lungs.OxygenatingGases, Is.Not.Empty,
                "Should have oxygenating gases while inserted");

            containerSys.Remove(lungEntity, organContainer);

            Assert.That(lungs.OxygenatingGases, Is.Empty,
                "Oxygenating gases should be cleared on removal");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Stomach - reduced hunger decay
    // ================================================================

    [Test]
    public async Task StomachReducesHungerDecayTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBodyWithStomach", mapData.GridCoords);
            var hunger = entMan.GetComponent<HungerComponent>(body);

            // CyberneticStomachComponent.DecayMultiplier is 0.5
            var defaultRate = 0.01666666666f;
            var expected = defaultRate * 0.5f;
            Assert.That(hunger.BaseDecayRate, Is.EqualTo(expected).Within(0.0001f),
                "Hunger decay should be halved with cybernetic stomach");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StomachRestoresDecayOnRemovalTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containerSys = entMan.System<SharedContainerSystem>();
            var body = entMan.SpawnEntity("CyberTestBodyWithStomach", mapData.GridCoords);
            var hunger = entMan.GetComponent<HungerComponent>(body);

            var reducedRate = hunger.BaseDecayRate;
            var defaultRate = 0.01666666666f;
            Assert.That(reducedRate, Is.LessThan(defaultRate),
                "Rate should be reduced with stomach");

            // Remove stomach
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            foreach (var ent in organContainer.ContainedEntities.ToList())
            {
                if (entMan.HasComponent<CyberneticStomachComponent>(ent))
                    containerSys.Remove(ent, organContainer);
            }

            Assert.That(hunger.BaseDecayRate, Is.EqualTo(defaultRate).Within(0.0001f),
                "Hunger decay should be restored to default after stomach removal");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Ears - deafness resistance
    // ================================================================

    [Test]
    public async Task EarsResistDeafnessTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBodyWithEars", mapData.GridCoords);

            var ev = new CanHearAttemptEvent();
            ev.Cancel();

            entMan.EventBus.RaiseLocalEvent(body, ev);

            Assert.That(ev.Cancelled, Is.False,
                "Cybernetic ears should uncancel deafness");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EarsNoResistanceWithoutOrganTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBody", mapData.GridCoords);

            var ev = new CanHearAttemptEvent();
            ev.Cancel();

            entMan.EventBus.RaiseLocalEvent(body, ev);

            Assert.That(ev.Cancelled, Is.True,
                "Deafness should persist without cybernetic ears");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Liver - overdose resistance
    // ================================================================

    [Test]
    public async Task LiverAppliesOverdoseResistanceOnInsertTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBodyWithLiver", mapData.GridCoords);

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.True,
                "Body should have OverdoseResistanceComponent after liver insertion");

            var resistance = entMan.GetComponent<OverdoseResistanceComponent>(body);
            Assert.That(resistance.ThresholdMultiplier, Is.EqualTo(1.5f),
                "Threshold multiplier should match liver's configured value");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LiverRemovesOverdoseResistanceOnRemovalTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containerSys = entMan.System<SharedContainerSystem>();
            var body = entMan.SpawnEntity("CyberTestBodyWithLiver", mapData.GridCoords);

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.True,
                "Precondition: resistance should be present");

            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            foreach (var ent in organContainer.ContainedEntities.ToList())
            {
                if (entMan.HasComponent<CyberneticLiverComponent>(ent))
                    containerSys.Remove(ent, organContainer);
            }

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.False,
                "OverdoseResistanceComponent should be removed with liver");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LiverNoResistanceWithoutOrganTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBody", mapData.GridCoords);

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.False,
                "Body without cybernetic liver should not have overdose resistance");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Dynamic insertion - organ added after spawn
    // ================================================================

    [Test]
    public async Task DynamicOrganInsertionTriggersEffectTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBody", mapData.GridCoords);

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.False);

            entMan.SpawnInContainerOrDrop("CyberTestLiver", body, BodyComponent.ContainerID);

            Assert.That(entMan.HasComponent<OverdoseResistanceComponent>(body), Is.True,
                "Dynamic organ insertion should trigger liver effect");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Heart — edge cases
    // ================================================================

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
            var body = entMan.SpawnEntity("CyberTestBodyWithHeart", mapData.GridCoords);

            // Transition to Dead (not Critical) — should not inject
            mobStateSys.ChangeMobState(body, MobState.Dead);

            var bloodstream = entMan.GetComponent<BloodstreamComponent>(body);
            Assert.That(containerSys.TryGetSolution(body, bloodstream.BloodSolutionName, out _, out var solution),
                Is.True);
            Assert.That(solution.GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.Zero),
                "Heart should not inject on state changes other than Critical");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Lungs — oxygenating gases preserved
    // ================================================================

    [Test]
    public async Task LungsPreserveOxygenatingGasesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBodyWithLungs", mapData.GridCoords);

            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            CyberneticLungsComponent? lungs = null;
            foreach (var organ in organContainer.ContainedEntities)
            {
                if (entMan.TryGetComponent(organ, out lungs))
                    break;
            }

            Assert.That(lungs, Is.Not.Null, "Precondition: lungs organ should exist");

            // Oxygen should be in the oxygenating set for a human metabolizer
            Assert.That(lungs!.OxygenatingGases.Contains(Gas.Oxygen), Is.True,
                "Oxygen should be in the oxygenating gases set for a human-type body");

            // Create a gas mix and raise InhaledGasEvent via relay
            var gasMix = new GasMixture(1f);
            gasMix.SetMoles(Gas.Oxygen, 10f);
            gasMix.SetMoles(Gas.Plasma, 10f);

            var inhaleEvent = new InhaledGasEvent(gasMix);
            var relayEvent = new BodyRelayedEvent<InhaledGasEvent>(
                new Entity<BodyComponent>(body, entMan.GetComponent<BodyComponent>(body)),
                inhaleEvent);

            // Find the organ entity to raise on
            EntityUid lungOrgan = default;
            foreach (var organ in organContainer.ContainedEntities)
            {
                if (entMan.HasComponent<CyberneticLungsComponent>(organ))
                {
                    lungOrgan = organ;
                    break;
                }
            }

            entMan.EventBus.RaiseLocalEvent(lungOrgan, ref relayEvent);

            // Oxygen (oxygenating) should be untouched
            Assert.That(gasMix.GetMoles(Gas.Oxygen), Is.EqualTo(10f),
                "Oxygenating gas (Oxygen) should not be filtered");

            // Plasma (toxic) should be reduced by FilterFraction (50%)
            Assert.That(gasMix.GetMoles(Gas.Plasma), Is.EqualTo(5f).Within(0.01f),
                "Toxic gas (Plasma) should be filtered by 50%");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Ears — cybernetic ears override temporary deafness
    // ================================================================

    [Test]
    public async Task EarsOverrideTemporaryDeafnessTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // Entity has DeafableComponent + TemporaryDeafness + CyberneticEars
            var body = entMan.SpawnEntity("CyberTestDeafBodyWithEars", mapData.GridCoords);

            var ev = new CanHearAttemptEvent();
            ev.Cancel(); // Simulate something trying to deafen
            entMan.EventBus.RaiseLocalEvent(body, ev);

            Assert.That(ev.Deaf, Is.False,
                "Cybernetic ears should override temporary deafness (uncancel the event)");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeafBodyWithoutEarsRemainsDeafTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // Entity has DeafableComponent + TemporaryDeafness but NO ears
            var body = entMan.SpawnEntity("CyberTestDeafBody", mapData.GridCoords);

            var ev = new CanHearAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(body, ev);

            Assert.That(ev.Deaf, Is.True,
                "Deaf entity without cybernetic ears should remain deaf");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Stomach — decay rate restored after remove + re-insert
    // ================================================================

    [Test]
    public async Task StomachDecayRateRestoredAfterRemoveAndReinsertTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBodyWithStomach", mapData.GridCoords);
            var hunger = entMan.GetComponent<HungerComponent>(body);
            var reducedRate = hunger.BaseDecayRate;

            // Remove the stomach organ
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            EntityUid stomachOrgan = default;
            foreach (var ent in organContainer.ContainedEntities.ToList())
            {
                if (entMan.HasComponent<CyberneticStomachComponent>(ent))
                {
                    stomachOrgan = ent;
                    containerSys.Remove(ent, organContainer);
                    break;
                }
            }

            var restoredRate = hunger.BaseDecayRate;
            Assert.That(restoredRate, Is.GreaterThan(reducedRate),
                "Removing stomach should restore the original higher decay rate");

            // Re-insert the same stomach organ
            containerSys.Insert(stomachOrgan, organContainer);

            Assert.That(hunger.BaseDecayRate, Is.EqualTo(reducedRate).Within(0.0001f),
                "Re-inserting stomach should re-apply the reduced decay rate");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Eyes — dynamic insertion provides flash protection
    // ================================================================

    [Test]
    public async Task EyesDynamicInsertionProvidesFlashProtectionTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBody", mapData.GridCoords);

            // Flash should work without eyes
            var flashBefore = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref flashBefore);
            Assert.That(flashBefore.Cancelled, Is.False,
                "Precondition: flash should not be cancelled without cybernetic eyes");

            // Dynamically insert cybernetic eyes
            entMan.SpawnInContainerOrDrop("CyberTestEyes", body, BodyComponent.ContainerID);

            var flashAfter = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref flashAfter);
            Assert.That(flashAfter.Cancelled, Is.True,
                "Dynamically inserted cybernetic eyes should provide flash protection");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Liver — multiplier value is correctly applied
    // ================================================================

    [Test]
    public async Task LiverOverdoseThresholdMultiplierMatchesComponentTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBodyWithLiver", mapData.GridCoords);
            var resistance = entMan.GetComponent<OverdoseResistanceComponent>(body);

            Assert.That(resistance.ThresholdMultiplier, Is.EqualTo(1.5f),
                "OverdoseResistanceComponent multiplier should match CyberneticLiverComponent default (1.5)");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Eyes — removal restores flash vulnerability
    // ================================================================

    [Test]
    public async Task EyesRemovalRestoresFlashVulnerabilityTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBodyWithEyes", mapData.GridCoords);

            // Verify flash is blocked with eyes
            var flashBefore = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref flashBefore);
            Assert.That(flashBefore.Cancelled, Is.True, "Precondition: flash should be cancelled with eyes");

            // Remove the eyes organ
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            foreach (var ent in organContainer.ContainedEntities.ToList())
            {
                if (entMan.HasComponent<CyberneticEyesComponent>(ent))
                    containerSys.Remove(ent, organContainer);
            }

            var flashAfter = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref flashAfter);
            Assert.That(flashAfter.Cancelled, Is.False,
                "Flash should not be cancelled after removing cybernetic eyes");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Ears — removal restores deafness vulnerability
    // ================================================================

    [Test]
    public async Task EarsRemovalRestoresDeafnessVulnerabilityTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestDeafBodyWithEars", mapData.GridCoords);

            // Verify ears override deafness
            var evBefore = new CanHearAttemptEvent();
            evBefore.Cancel();
            entMan.EventBus.RaiseLocalEvent(body, evBefore);
            Assert.That(evBefore.Cancelled, Is.False, "Precondition: ears should uncancel deafness");

            // Remove the ears organ
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            foreach (var ent in organContainer.ContainedEntities.ToList())
            {
                if (entMan.HasComponent<CyberneticEarsComponent>(ent))
                    containerSys.Remove(ent, organContainer);
            }

            var evAfter = new CanHearAttemptEvent();
            evAfter.Cancel();
            entMan.EventBus.RaiseLocalEvent(body, evAfter);
            Assert.That(evAfter.Cancelled, Is.True,
                "Deafness should persist after removing cybernetic ears");
        });

        await pair.CleanReturnAsync();
    }

    // ================================================================
    // Lungs — dynamic insertion resolves oxygenating gases
    // ================================================================

    [Test]
    public async Task LungsDynamicInsertionResolvesOxygenatingGasesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberTestBody", mapData.GridCoords);

            // Dynamically insert lungs
            var lungEntity = entMan.SpawnInContainerOrDrop("CyberTestLungs", body, BodyComponent.ContainerID);

            var lungs = entMan.GetComponent<CyberneticLungsComponent>(lungEntity);
            Assert.That(lungs.OxygenatingGases, Is.Not.Empty,
                "Dynamically inserted lungs should resolve oxygenating gases");
            Assert.That(lungs.OxygenatingGases.Contains(Gas.Oxygen), Is.True,
                "Oxygen should be in oxygenating gases after dynamic insertion");
        });

        await pair.CleanReturnAsync();
    }
}

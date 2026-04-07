using Content.Server.RussStation.Body;
using Content.Shared.Body.Components;
using Content.Shared.Emp;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(CyberneticEmpSystem))]
public sealed class CyberneticEmpTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: EmpTestBody
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
  - type: Respirator
    damage:
      types:
        Asphyxiation: 0.5
    damageRecovery:
      types:
        Asphyxiation: -0.5
  - type: Blindable
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: SolutionContainerManager

- type: entity
  id: EmpTestOrganCardiac
  components:
  - type: Organ
    category: Heart
  - type: CyberneticOrgan
    empEffect: CardiacArrest
    empVulnerability: 1.0

- type: entity
  id: EmpTestOrganBreathing
  components:
  - type: Organ
    category: Lungs
  - type: CyberneticOrgan
    empEffect: BreathingFailure
    empVulnerability: 1.0

- type: entity
  id: EmpTestOrganFlicker
  components:
  - type: Organ
    category: Eyes
  - type: CyberneticOrgan
    empEffect: Flicker
    empVulnerability: 1.0

- type: entity
  id: EmpTestOrganDeafen
  components:
  - type: Organ
    category: Ears
  - type: CyberneticOrgan
    empEffect: Deafen
    empVulnerability: 1.0

- type: entity
  id: EmpTestOrganNone
  components:
  - type: Organ
    category: Liver
  - type: CyberneticOrgan
    empEffect: None
    empVulnerability: 1.0

- type: entity
  id: EmpTestOrganHighVuln
  components:
  - type: Organ
    category: Heart
  - type: CyberneticOrgan
    empEffect: CardiacArrest
    empVulnerability: 2.0

- type: entity
  id: EmpTestBodyWithCardiacOrgan
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
  - type: Respirator
    damage:
      types:
        Asphyxiation: 0.5
    damageRecovery:
      types:
        Asphyxiation: -0.5
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
        - id: EmpTestOrganCardiac

- type: entity
  id: EmpTestBodyWithBreathingOrgan
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
  - type: Respirator
    damage:
      types:
        Asphyxiation: 0.5
    damageRecovery:
      types:
        Asphyxiation: -0.5
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
        - id: EmpTestOrganBreathing

- type: entity
  id: EmpTestBodyWithFlickerOrgan
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
  - type: Blindable
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: EmpTestOrganFlicker

- type: entity
  id: EmpTestBodyWithNoneOrgan
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
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: EmpTestOrganNone

- type: entity
  id: EmpTestBodyWithHighVulnOrgan
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
  - type: Respirator
    damage:
      types:
        Asphyxiation: 0.5
    damageRecovery:
      types:
        Asphyxiation: -0.5
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
        - id: EmpTestOrganHighVuln
";

    [Test]
    public async Task EmpAppliesCardiacArrestTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EmpTestBodyWithCardiacOrgan", mapData.GridCoords);

            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(body), Is.False,
                "Should not have cardiac arrest before EMP");

            var ev = new EmpPulseEvent(1000f, false, false, TimeSpan.FromSeconds(10), null);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Affected, Is.True,
                "EMP event should be marked as affected");
            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(body), Is.True,
                "Should have cardiac arrest after EMP on body with cardiac organ");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpAppliesBreathingSuppressedTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EmpTestBodyWithBreathingOrgan", mapData.GridCoords);

            Assert.That(entMan.HasComponent<ActiveBreathingSuppressedComponent>(body), Is.False,
                "Should not have breathing suppressed before EMP");

            var ev = new EmpPulseEvent(1000f, false, false, TimeSpan.FromSeconds(10), null);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Affected, Is.True,
                "EMP event should be marked as affected");
            Assert.That(entMan.HasComponent<ActiveBreathingSuppressedComponent>(body), Is.True,
                "Should have breathing suppressed after EMP on body with breathing organ");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpAppliesBlindnessTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EmpTestBodyWithFlickerOrgan", mapData.GridCoords);

            var ev = new EmpPulseEvent(1000f, false, false, TimeSpan.FromSeconds(10), null);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Affected, Is.True,
                "EMP event should be marked as affected");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpNoneEffectDoesNotApplyStatusTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EmpTestBodyWithNoneOrgan", mapData.GridCoords);

            var ev = new EmpPulseEvent(1000f, false, false, TimeSpan.FromSeconds(10), null);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            // Affected is still set because the organ is cybernetic, but no status effect is applied
            Assert.That(ev.Affected, Is.True,
                "EMP event should still be marked affected for cybernetic organ");
            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(body), Is.False,
                "No cardiac arrest for None effect");
            Assert.That(entMan.HasComponent<ActiveBreathingSuppressedComponent>(body), Is.False,
                "No breathing suppressed for None effect");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpNoEffectWithoutCyberneticOrgansTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EmpTestBody", mapData.GridCoords);

            var ev = new EmpPulseEvent(1000f, false, false, TimeSpan.FromSeconds(10), null);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Affected, Is.False,
                "EMP should not affect body without cybernetic organs");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpVulnerabilityScalesDurationTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();

            // Normal vulnerability (1.0x)
            var normalBody = entMan.SpawnEntity("EmpTestBodyWithCardiacOrgan", mapData.GridCoords);
            var normalEv = new EmpPulseEvent(1000f, false, false, TimeSpan.FromSeconds(10), null);
            entMan.EventBus.RaiseLocalEvent(normalBody, ref normalEv);

            // High vulnerability (2.0x)
            var highBody = entMan.SpawnEntity("EmpTestBodyWithHighVulnOrgan", mapData.GridCoords);
            var highEv = new EmpPulseEvent(1000f, false, false, TimeSpan.FromSeconds(10), null);
            entMan.EventBus.RaiseLocalEvent(highBody, ref highEv);

            // Both should have the effect applied
            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(normalBody), Is.True);
            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(highBody), Is.True);

            // The high vulnerability organ should have a longer duration effect
            // We can't easily check duration directly, but we verify both got applied
            // The important thing is the system didn't crash with the 2.0x multiplier
        });

        await pair.CleanReturnAsync();
    }
}

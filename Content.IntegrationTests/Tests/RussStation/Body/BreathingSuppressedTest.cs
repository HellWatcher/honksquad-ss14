using Content.Server.Body.Systems;
using Content.Server.RussStation.Body;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(BreathingSuppressedSystem))]
public sealed class BreathingSuppressedTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: BreathSuppTestBody
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
";

    private const string EffectProto = "StatusEffectBreathingSuppressed";

    [Test]
    public async Task ApplyingEffectAddsActiveComponentTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();
            var body = entMan.SpawnEntity("BreathSuppTestBody", mapData.GridCoords);

            Assert.That(entMan.HasComponent<ActiveBreathingSuppressedComponent>(body), Is.False,
                "Should not have active component before effect");

            var added = statusSys.TryAddStatusEffectDuration(body, EffectProto, TimeSpan.FromSeconds(10));
            Assert.That(added, Is.True, "Should successfully apply breathing suppressed effect");
            Assert.That(entMan.HasComponent<ActiveBreathingSuppressedComponent>(body), Is.True,
                "Should have active component after effect applied");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingEffectRemovesActiveComponentTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        EntityUid body = default;

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();
            body = entMan.SpawnEntity("BreathSuppTestBody", mapData.GridCoords);

            statusSys.TryAddStatusEffectDuration(body, EffectProto, TimeSpan.FromSeconds(10));
            Assert.That(entMan.HasComponent<ActiveBreathingSuppressedComponent>(body), Is.True);

            statusSys.TryRemoveStatusEffect(body, EffectProto);
        });

        // PredictedQueueDel is deferred
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<ActiveBreathingSuppressedComponent>(body), Is.False,
                "Active component should be removed when effect is removed");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InhaledGasIsHandledWhenActiveTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();
            var body = entMan.SpawnEntity("BreathSuppTestBody", mapData.GridCoords);

            statusSys.TryAddStatusEffectDuration(body, EffectProto, TimeSpan.FromSeconds(10));
            Assert.That(entMan.HasComponent<ActiveBreathingSuppressedComponent>(body), Is.True);

            // Raise InhaledGasEvent on the body - breathing suppressed should mark it handled
            var gasMix = new GasMixture(10f);
            gasMix.SetMoles(Gas.Oxygen, 10f);
            var ev = new InhaledGasEvent(gasMix);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Handled, Is.True,
                "InhaledGasEvent should be handled when breathing is suppressed");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InhaledGasNotHandledWithoutEffectTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("BreathSuppTestBody", mapData.GridCoords);

            Assert.That(entMan.HasComponent<ActiveBreathingSuppressedComponent>(body), Is.False);

            var gasMix = new GasMixture(10f);
            gasMix.SetMoles(Gas.Oxygen, 10f);
            var ev = new InhaledGasEvent(gasMix);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Handled, Is.False,
                "InhaledGasEvent should not be handled without breathing suppression");
        });

        await pair.CleanReturnAsync();
    }
}

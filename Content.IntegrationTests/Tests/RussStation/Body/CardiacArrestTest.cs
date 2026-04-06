using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Medical;
using Content.Server.RussStation.Body;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(CardiacArrestSystem))]
public sealed class CardiacArrestTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CardiacTestBody
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
";

    private const string EffectProto = "StatusEffectCardiacArrest";

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
            var body = entMan.SpawnEntity("CardiacTestBody", mapData.GridCoords);

            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(body), Is.False,
                "Should not have ActiveCardiacArrestComponent before effect");

            var added = statusSys.TryAddStatusEffectDuration(body, EffectProto, TimeSpan.FromSeconds(10));
            Assert.That(added, Is.True, "Should successfully apply cardiac arrest effect");
            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(body), Is.True,
                "Should have ActiveCardiacArrestComponent after effect applied");
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
            body = entMan.SpawnEntity("CardiacTestBody", mapData.GridCoords);

            statusSys.TryAddStatusEffectDuration(body, EffectProto, TimeSpan.FromSeconds(10));
            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(body), Is.True);

            statusSys.TryRemoveStatusEffect(body, EffectProto);
        });

        // PredictedQueueDel is deferred, let deletion process
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(body), Is.False,
                "ActiveCardiacArrestComponent should be removed when effect is removed");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UpdateDrainsSaturationTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();
        EntityUid body = default;
        float satBefore = 0f;

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();
            body = entMan.SpawnEntity("CardiacTestBody", mapData.GridCoords);

            statusSys.TryAddStatusEffectDuration(body, EffectProto, TimeSpan.FromSeconds(30));
            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(body), Is.True);

            var respirator = entMan.GetComponent<RespiratorComponent>(body);
            satBefore = respirator.Saturation;
        });

        // Run several ticks so CardiacArrestSystem.Update drains saturation
        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            var respirator = entMan.GetComponent<RespiratorComponent>(body);
            Assert.That(respirator.Saturation, Is.LessThan(satBefore),
                "Cardiac arrest should drain respirator saturation over time");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DefibrillatorRemovesCardiacArrestTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();
            var body = entMan.SpawnEntity("CardiacTestBody", mapData.GridCoords);

            statusSys.TryAddStatusEffectDuration(body, EffectProto, TimeSpan.FromSeconds(30));
            Assert.That(entMan.HasComponent<ActiveCardiacArrestComponent>(body), Is.True);

            // Simulate defibrillator zap by raising the event directly
            var defibEnt = entMan.SpawnEntity(null, mapData.GridCoords);
            var defibComp = entMan.EnsureComponent<DefibrillatorComponent>(defibEnt);
            var ev = new TargetDefibrillatedEvent(defibEnt, (defibEnt, defibComp));
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            // QueueDel is deferred, so the active component won't be gone until next tick
        });

        // Let deferred deletions process
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            // We can't check the body entity directly since QueueDel might have cascading effects,
            // but we can verify via the status effect system
            // Actually, let's just re-check - the body should still exist, only the effect entity is deleted
            var query = entMan.EntityQueryEnumerator<ActiveCardiacArrestComponent>();
            var found = false;
            while (query.MoveNext(out _, out _))
            {
                found = true;
            }
            Assert.That(found, Is.False,
                "ActiveCardiacArrestComponent should be removed after defibrillation");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HealthAnalyzerReportsCardiacArrestTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();
            var analyzerSys = entMan.System<HealthAnalyzerSystem>();
            var body = entMan.SpawnEntity("CardiacTestBody", mapData.GridCoords);

            // Without cardiac arrest
            var stateBefore = analyzerSys.GetHealthAnalyzerUiState(body);
            Assert.That(stateBefore.CardiacArrest, Is.False,
                "Health analyzer should not report cardiac arrest before effect");

            // With cardiac arrest
            statusSys.TryAddStatusEffectDuration(body, EffectProto, TimeSpan.FromSeconds(10));
            var stateAfter = analyzerSys.GetHealthAnalyzerUiState(body);
            Assert.That(stateAfter.CardiacArrest, Is.True,
                "Health analyzer should report cardiac arrest when effect is active");
        });

        await pair.CleanReturnAsync();
    }
}

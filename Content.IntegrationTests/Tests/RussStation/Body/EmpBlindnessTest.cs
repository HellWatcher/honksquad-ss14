using Content.Server.RussStation.Body;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(EmpBlindnessSystem))]
public sealed class EmpBlindnessTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: EmpBlindTestBody
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
";

    private const string EffectProto = "StatusEffectTemporaryBlindness";

    [Test]
    public async Task ApplyingEffectAddsTemporaryBlindnessTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();
            var body = entMan.SpawnEntity("EmpBlindTestBody", mapData.GridCoords);

            Assert.That(statusSys.HasEffectComp<BlindnessStatusEffectComponent>(body), Is.False,
                "Should not have a blindness status effect before the EMP effect");
            Assert.That(entMan.GetComponent<BlindableComponent>(body).IsBlind, Is.False,
                "Should not be blind before the EMP effect");

            var added = statusSys.TryAddStatusEffectDuration(body, EffectProto, TimeSpan.FromSeconds(10));
            Assert.That(added, Is.True, "Should successfully apply EMP blindness effect");
            Assert.That(statusSys.HasEffectComp<BlindnessStatusEffectComponent>(body), Is.True,
                "EMP effect entity should carry BlindnessStatusEffectComponent once applied");
            Assert.That(entMan.GetComponent<BlindableComponent>(body).IsBlind, Is.True,
                "Should be blind while the EMP effect is applied");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingEffectRemovesTemporaryBlindnessTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        EntityUid body = default;

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();
            body = entMan.SpawnEntity("EmpBlindTestBody", mapData.GridCoords);

            statusSys.TryAddStatusEffectDuration(body, EffectProto, TimeSpan.FromSeconds(10));
            Assert.That(statusSys.HasEffectComp<BlindnessStatusEffectComponent>(body), Is.True);
            Assert.That(entMan.GetComponent<BlindableComponent>(body).IsBlind, Is.True);

            statusSys.TryRemoveStatusEffect(body, EffectProto);
        });

        // PredictedQueueDel is deferred
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();
            Assert.That(statusSys.HasEffectComp<BlindnessStatusEffectComponent>(body), Is.False,
                "Blindness status effect should be gone when the EMP effect is removed");
            Assert.That(entMan.GetComponent<BlindableComponent>(body).IsBlind, Is.False,
                "Should no longer be blind when the EMP effect is removed");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoBlindnessWithoutEffectTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var statusSys = entMan.System<StatusEffectsSystem>();
            var body = entMan.SpawnEntity("EmpBlindTestBody", mapData.GridCoords);

            Assert.That(statusSys.HasEffectComp<BlindnessStatusEffectComponent>(body), Is.False,
                "Body without the EMP blindness effect should not have a blindness status effect");
            Assert.That(entMan.GetComponent<BlindableComponent>(body).IsBlind, Is.False,
                "Body without the EMP blindness effect should not be blind");
        });

        await pair.CleanReturnAsync();
    }
}

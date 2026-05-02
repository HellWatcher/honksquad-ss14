using Content.Shared.RussStation.Hearing;
using Content.Shared.RussStation.Hearing.Systems;
using Content.Shared.RussStation.Trigger;
using Content.Shared.Trigger;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Trigger;

[TestFixture]
[TestOf(typeof(DeafenOnTriggerSystem))]
public sealed class DeafenOnTriggerTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DeafenTriggerTestSource
  components:
  - type: DeafenOnTrigger
    range: 5
    duration: 8

- type: entity
  id: DeafenTriggerTestVictim
  components:
  - type: Deafable
  - type: MobState
    allowedStates:
    - Alive
  - type: MobThresholds
    thresholds:
      0: Alive
  - type: Damageable
";

    [Test]
    public async Task DeafenOnTriggerDeafensInRangeTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var deafableSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DeafableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var source = entMan.SpawnEntity("DeafenTriggerTestSource", mapData.GridCoords);
            var victim = entMan.SpawnEntity("DeafenTriggerTestVictim", mapData.GridCoords);

            Assert.That(entMan.GetComponent<DeafableComponent>(victim).IsDeaf, Is.False,
                "Precondition: victim hears at spawn");

            // Synthesize the same event the trigger pipeline raises.
            var ev = new TriggerEvent(User: null, Key: null);
            entMan.EventBus.RaiseLocalEvent(source, ref ev);

            // UpdateIsDeaf to recompute against the newly-applied status.
            deafableSys.UpdateIsDeaf(victim);

            Assert.That(entMan.GetComponent<DeafableComponent>(victim).IsDeaf, Is.True,
                "Victim in range should be deaf after the trigger fires");
        });

        await pair.CleanReturnAsync();
    }
}

using System.Linq;
using Content.Server.Radio;
using Content.Server.RussStation.Hearing;
using Content.Shared.Radio;
using Content.Shared.RussStation.Hearing;
using Content.Shared.RussStation.Hearing.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Hearing;

[TestFixture]
[TestOf(typeof(DeafRadioBlockSystem))]
public sealed class DeafRadioBlockTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DeafRadioTestHearing
  components:
  - type: Deafable

- type: entity
  id: DeafRadioTestDeaf
  components:
  - type: Deafable
  - type: TemporaryDeafness
";

    [Test]
    public async Task DeafEntityCancelsRadioReceiveTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var deafableSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DeafableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("DeafRadioTestDeaf", mapData.GridCoords);
            // TemporaryDeafnessComponent's CanHearAttempt handler cancels hearing.
            deafableSys.UpdateIsDeaf(ent);

            var deafable = entMan.GetComponent<DeafableComponent>(ent);
            Assert.That(deafable.IsDeaf, Is.True, "Precondition: TemporaryDeafness should make entity deaf");

            var channel = protoMan.EnumeratePrototypes<RadioChannelPrototype>().First();
            var attempt = new RadioReceiveAttemptEvent(channel, ent, ent);
            entMan.EventBus.RaiseLocalEvent(ent, ref attempt);

            Assert.That(attempt.Cancelled, Is.True,
                "Radio receive should be cancelled when DeafableComponent.IsDeaf is true");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HearingEntityReceivesRadioTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("DeafRadioTestHearing", mapData.GridCoords);

            var channel = protoMan.EnumeratePrototypes<RadioChannelPrototype>().First();
            var attempt = new RadioReceiveAttemptEvent(channel, ent, ent);
            entMan.EventBus.RaiseLocalEvent(ent, ref attempt);

            Assert.That(attempt.Cancelled, Is.False,
                "Radio receive should pass when IsDeaf is false");
        });

        await pair.CleanReturnAsync();
    }
}

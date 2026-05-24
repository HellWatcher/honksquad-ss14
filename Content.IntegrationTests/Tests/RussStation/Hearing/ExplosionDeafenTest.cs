using System.Collections.Generic;
using Content.Server.RussStation.Hearing;
using Content.Shared.Damage;
using Content.Shared.Explosion;
using Content.Shared.RussStation.Hearing;
using Content.Shared.RussStation.Hearing.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Hearing;

[TestFixture]
[TestOf(typeof(ExplosionDeafenSystem))]
public sealed class ExplosionDeafenTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExplodeDeafTestEntity
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
    public async Task ExplosionDeafensVictimTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var deafableSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DeafableSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ent = entMan.SpawnEntity("ExplodeDeafTestEntity", mapData.GridCoords);
            var deafableBefore = entMan.GetComponent<DeafableComponent>(ent);
            Assert.That(deafableBefore.IsDeaf, Is.False, "Precondition: entity hears at spawn");

            // Synthesize the same event ExplosionSystem.Processing raises on each victim.
            var ev = new BeforeExplodeEvent(new DamageSpecifier(), "Default", new List<EntityUid>());
            entMan.EventBus.RaiseLocalEvent(ent, ref ev);

            // Deafening goes through the new status effect system; force a recompute and check.
            deafableSys.UpdateIsDeaf(ent);

            Assert.That(entMan.GetComponent<DeafableComponent>(ent).IsDeaf, Is.True,
                "Entity should be deaf after BeforeExplodeEvent");
        });

        await pair.CleanReturnAsync();
    }
}

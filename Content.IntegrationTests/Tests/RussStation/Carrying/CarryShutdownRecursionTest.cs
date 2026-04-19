using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.RussStation.Carrying.Components;
using Content.Shared.RussStation.Carrying.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Carrying;

/// <summary>
/// Regression test for the mutual-recursion crash in paired carry markers.
/// <see cref="ActiveCarrierComponent"/> and <see cref="BeingCarriedComponent"/> each
/// reference the other in their shutdown handler. Before the fix, both handlers used
/// <c>HasComp</c>, which stays true throughout <c>ComponentShutdown</c>, so removing
/// one side re-entered the other's handler and stack-overflowed. The fix gates the
/// mirror removal on <c>LifeStage &lt; Stopping</c>.
///
/// Only the target-side entry point is exercised here. The carrier-side entry point
/// would also be valuable, but its teardown calls <c>PlaceNextTo</c> which debug-asserts
/// on the synthetic test fixtures — unrelated to the recursion guard itself.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedCarryingSystem))]
public sealed class CarryShutdownRecursionTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CarryRecursionCarrier
  components:
  - type: Carrier
  - type: Carriable
  - type: Hands
  - type: ComplexInteraction
  - type: InputMover
  - type: Physics
    bodyType: KinematicController
  - type: Puller
  - type: StandingState
  - type: MobState
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Dead
  - type: Damageable
    damageContainer: Biological
  - type: Body
    prototype: Human
  - type: Fixtures
    fixtures:
      fix1:
        shape: !type:PhysShapeCircle
          radius: 0.35

- type: entity
  id: CarryRecursionTarget
  components:
  - type: Carriable
  - type: Physics
    bodyType: KinematicController
  - type: StandingState
  - type: MobState
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Dead
  - type: Damageable
    damageContainer: Biological
  - type: Body
    prototype: Human
  - type: Pullable
  - type: Fixtures
    fixtures:
      fix1:
        shape: !type:PhysShapeCircle
          radius: 0.35
";

    /// <summary>
    /// Remove the target-side marker directly. The carrier-side handler must clean up
    /// without re-entering the target handler. Before the fix this stack-overflowed.
    /// </summary>
    [Test]
    public async Task RemoveBeingCarriedDropsActiveCarrierWithoutRecursion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var carrying = server.System<SharedCarryingSystem>();
        var mobState = server.System<MobStateSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var carrier = entityManager.SpawnEntity("CarryRecursionCarrier", mapData.GridCoords);
            var target = entityManager.SpawnEntity("CarryRecursionTarget", mapData.GridCoords);

            mobState.ChangeMobState(target, MobState.Critical);
            carrying.Carry(carrier, target);

            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.True);
            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.True);

            Assert.DoesNotThrow(() => entityManager.RemoveComponent<BeingCarriedComponent>(target));

            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.False);
            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.False,
                "Carrier-side marker should have been cleaned up by the shutdown cascade");
        });

        await pair.CleanReturnAsync();
    }
}

using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.RussStation.Carrying.Components;
using Content.Shared.RussStation.Carrying.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Carrying;

[TestFixture]
[TestOf(typeof(SharedCarryingSystem))]
public sealed class CarryInterruptTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CarryInterruptCarrier
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
  # Upstream moved damageContainer off Damageable onto Injurable; without it the
  # entity has no damage container and can be neither damaged nor healed.
  - type: Injurable
    damageContainer: Biological
  - type: Body
    prototype: Human
  - type: Fixtures
    fixtures:
      fix1:
        shape: !type:PhysShapeCircle
          radius: 0.35

- type: entity
  id: CarryInterruptTarget
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
  # Upstream moved damageContainer off Damageable onto Injurable; without it the
  # entity has no damage container and can be neither damaged nor healed.
  - type: Injurable
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
    /// Completion path: InterruptCarry ends the carry, stuns the carrier, and is safe
    /// to call when no carry is in progress.
    /// </summary>
    [Test]
    public async Task InterruptEndsCarryAndStunsCarrier()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var carrying = server.System<SharedCarryingSystem>();
        var mobState = server.System<MobStateSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var carrier = entityManager.SpawnEntity("CarryInterruptCarrier", mapData.GridCoords);
            var target = entityManager.SpawnEntity("CarryInterruptTarget", mapData.GridCoords);
            var bystander = entityManager.SpawnEntity("CarryInterruptCarrier", mapData.GridCoords);

            mobState.ChangeMobState(target, MobState.Critical);
            carrying.Carry(carrier, target);

            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.True);
            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.True);

            carrying.InterruptCarry(bystander, target);

            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.False,
                "Interrupt must tear down the carrier-side marker");
            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.False,
                "Interrupt must tear down the target-side marker");
            Assert.That(entityManager.HasComponent<StunnedComponent>(carrier), Is.True,
                "Interrupt must stun the carrier");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// If the carry already ended before the DoAfter completes, the interrupt must
    /// no-op rather than throw or create stray effects on the former carrier.
    /// </summary>
    [Test]
    public async Task InterruptWithoutActiveCarryNoOps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var carrying = server.System<SharedCarryingSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var target = entityManager.SpawnEntity("CarryInterruptTarget", mapData.GridCoords);
            var bystander = entityManager.SpawnEntity("CarryInterruptCarrier", mapData.GridCoords);

            Assert.DoesNotThrow(() => carrying.InterruptCarry(bystander, target),
                "Interrupting a non-carried target must be safe");
            Assert.That(entityManager.HasComponent<StunnedComponent>(bystander), Is.False,
                "No-op path must not stun anyone");
        });

        await pair.CleanReturnAsync();
    }
}

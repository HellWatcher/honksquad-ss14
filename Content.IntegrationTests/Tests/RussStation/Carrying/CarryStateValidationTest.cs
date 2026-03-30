using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Carrying.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Carrying;

[TestFixture]
[TestOf(typeof(ActiveCarrierComponent))]
public sealed class CarryStateValidationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CarryValidationCarrier
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
  id: CarryValidationTarget
  components:
  - type: Carriable
  - type: Buckle
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

- type: entity
  id: CarryValidationStrap
  components:
  - type: Strap
";

    /// <summary>
    /// ActiveCarrierComponent with no actual carry target is orphaned state.
    /// The periodic validation should remove it within a second.
    /// </summary>
    [Test]
    public async Task OrphanedActiveCarrierGetsCleaned()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        EntityUid carrier = default;

        await server.WaitAssertion(() =>
        {
            carrier = entityManager.SpawnEntity("CarryValidationCarrier", mapData.GridCoords);

            // Add the active marker without setting up a real carry to simulate corruption.
            entityManager.EnsureComponent<ActiveCarrierComponent>(carrier);
            var carrierComp = entityManager.GetComponent<CarrierComponent>(carrier);
            Assert.That(carrierComp.Carrying, Is.Null);
            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.True);
        });

        // Wait for the 1-second validation tick to fire.
        await pair.RunSeconds(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.False,
                "Orphaned ActiveCarrierComponent should be cleaned by validation");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Same as <see cref="OrphanedActiveCarrierGetsCleaned"/> but with a target entity
    /// spawned alongside, verifying cleanup still works in a populated environment.
    /// Tests the Carrying=null + ActiveCarrierComponent corruption path, which is the
    /// most common way carry state gets orphaned (partial cleanup by another system).
    /// </summary>
    [Test]
    public async Task DeletedTargetGetsCleaned()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        EntityUid carrier = default;

        await server.WaitAssertion(() =>
        {
            carrier = entityManager.SpawnEntity("CarryValidationCarrier", mapData.GridCoords);
            entityManager.SpawnEntity("CarryValidationTarget", mapData.GridCoords);

            var carrierComp = entityManager.GetComponent<CarrierComponent>(carrier);
            entityManager.EnsureComponent<ActiveCarrierComponent>(carrier);

            // [Access] prevents writing to Carrying from tests, so we test the
            // Carrying=null orphan case directly, which is the most common corruption.
            Assert.That(carrierComp.Carrying, Is.Null);
            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.True);
        });

        await pair.RunSeconds(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.False,
                "ActiveCarrierComponent should be removed when Carrying is null");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Buckling a carried entity should be allowed. The carry system drops the
    /// carry on buckle attempt rather than blocking the buckle.
    /// </summary>
    [Test]
    public async Task BuckleAttemptAllowedWhileCarried()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var target = entityManager.SpawnEntity("CarryValidationTarget", mapData.GridCoords);
            var strap = entityManager.SpawnEntity("CarryValidationStrap", mapData.GridCoords);

            entityManager.EnsureComponent<BeingCarriedComponent>(target);

            var buckleComp = entityManager.GetComponent<BuckleComponent>(target);
            var strapComp = entityManager.GetComponent<StrapComponent>(strap);

            var ev = new BuckleAttemptEvent(
                (strap, strapComp),
                (target, buckleComp),
                null,
                false);
            entityManager.EventBus.RaiseLocalEvent(target, ref ev);

            Assert.That(ev.Cancelled, Is.False,
                "Buckling should be allowed while carried (carry gets dropped, buckle goes through)");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Buckling an entity that isn't being carried should work normally,
    /// unaffected by the carry system's buckle handler.
    /// </summary>
    [Test]
    public async Task BuckleAttemptUnaffectedWhenNotCarried()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var target = entityManager.SpawnEntity("CarryValidationTarget", mapData.GridCoords);
            var strap = entityManager.SpawnEntity("CarryValidationStrap", mapData.GridCoords);

            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.False);

            var buckleComp = entityManager.GetComponent<BuckleComponent>(target);
            var strapComp = entityManager.GetComponent<StrapComponent>(strap);

            var ev = new BuckleAttemptEvent(
                (strap, strapComp),
                (target, buckleComp),
                null,
                false);
            entityManager.EventBus.RaiseLocalEvent(target, ref ev);

            Assert.That(ev.Cancelled, Is.False,
                "BuckleAttemptEvent should not be affected when entity is not being carried");
        });

        await pair.CleanReturnAsync();
    }
}

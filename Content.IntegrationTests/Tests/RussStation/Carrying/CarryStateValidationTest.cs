using Content.Shared.Buckle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.RussStation.Carrying.Components;
using Content.Shared.RussStation.Carrying.Systems;
using Content.Shared.Standing;
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
    /// Deleting the target entity mid-carry must not leave the carrier holding a stale
    /// ActiveCarrierComponent. Previously this depended on shutdown order between
    /// CarriableComponent and BeingCarriedComponent and could leave an orphan that a
    /// periodic validation tick had to clean up; the marker now owns its own carrier
    /// reference so the shutdown path no longer races.
    /// </summary>
    [Test]
    public async Task TargetDeletionCleansCarrierState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var carrying = server.System<SharedCarryingSystem>();
        var mobState = server.System<MobStateSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid carrier = default;

        await server.WaitAssertion(() =>
        {
            carrier = entityManager.SpawnEntity("CarryValidationCarrier", mapData.GridCoords);
            var target = entityManager.SpawnEntity("CarryValidationTarget", mapData.GridCoords);

            // Carry() requires the target to be incapacitated.
            mobState.ChangeMobState(target, MobState.Critical);

            carrying.Carry(carrier, target);
            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.True,
                "Carry() should have wired up the carrier marker");
            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.True,
                "Carry() should have wired up the target marker");

            entityManager.DeleteEntity(target);

            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.False,
                "Deleting the target must remove the carrier marker, regardless of component shutdown order");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Symmetric case: deleting the carrier must clear the target's BeingCarriedComponent
    /// without leaving an orphan, so the target can be carried again by someone else.
    /// </summary>
    [Test]
    public async Task CarrierDeletionCleansTargetState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var carrying = server.System<SharedCarryingSystem>();
        var mobState = server.System<MobStateSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            var carrier = entityManager.SpawnEntity("CarryValidationCarrier", mapData.GridCoords);
            target = entityManager.SpawnEntity("CarryValidationTarget", mapData.GridCoords);

            mobState.ChangeMobState(target, MobState.Critical);

            carrying.Carry(carrier, target);
            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.True);

            entityManager.DeleteEntity(carrier);

            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.False,
                "Deleting the carrier must remove the target marker, regardless of component shutdown order");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Dropping a live carry (both entities alive, not terminating) must run the
    /// teardown cleanly. OnBeingCarriedShutdown calls PlaceNextTo to reparent the
    /// target away from the carrier, which fires a parent-change message mid-shutdown.
    /// Without a LifeStage guard on OnCarriedParentChanged that reparent re-enters
    /// Drop and trips LifeShutdown's debug assert when it tries to RemComp an
    /// already-Stopping ActiveCarrierComponent.
    /// </summary>
    [Test]
    public async Task DropLiveCarryDoesNotRecurse()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var carrying = server.System<SharedCarryingSystem>();
        var mobState = server.System<MobStateSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var carrier = entityManager.SpawnEntity("CarryValidationCarrier", mapData.GridCoords);
            var target = entityManager.SpawnEntity("CarryValidationTarget", mapData.GridCoords);

            mobState.ChangeMobState(target, MobState.Critical);
            carrying.Carry(carrier, target);

            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.True);
            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.True);

            Assert.DoesNotThrow(() => carrying.Drop(carrier),
                "Drop on a live carry must not recurse through OnCarriedParentChanged when PlaceNextTo reparents the target mid-shutdown");

            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.False,
                "Carrier marker must be cleared after Drop");
            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.False,
                "Target marker must be cleared after Drop");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A DownedEvent on the carrier (fired e.g. from buckling the carrier into a chair
    /// via StandingStateSystem.Down) triggers Drop. This exercises the same mid-shutdown
    /// reparent as the fireman-carry crash path but entered from OnCarrierDowned rather
    /// than OnVirtualItemDeleted.
    /// </summary>
    [Test]
    public async Task CarrierDownedDropsCarryCleanly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var carrying = server.System<SharedCarryingSystem>();
        var mobState = server.System<MobStateSystem>();
        var standing = server.System<StandingStateSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var carrier = entityManager.SpawnEntity("CarryValidationCarrier", mapData.GridCoords);
            var target = entityManager.SpawnEntity("CarryValidationTarget", mapData.GridCoords);

            mobState.ChangeMobState(target, MobState.Critical);
            carrying.Carry(carrier, target);

            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.True);

            Assert.DoesNotThrow(() => standing.Down(carrier),
                "Downing the carrier must cleanly drop the carry without recursing through OnCarriedParentChanged");

            Assert.That(entityManager.HasComponent<ActiveCarrierComponent>(carrier), Is.False);
            Assert.That(entityManager.HasComponent<BeingCarriedComponent>(target), Is.False);
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

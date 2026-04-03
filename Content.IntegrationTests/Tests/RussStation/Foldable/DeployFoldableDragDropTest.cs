using Content.Shared.DragDrop;
using Content.Shared.Foldable;
using Content.Shared.Hands.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Foldable;

[TestFixture]
[TestOf(typeof(DeployFoldableSystem))]
public sealed class DeployFoldableDragDropTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: FoldableDragDropTestUser
  components:
  - type: Hands
  - type: ComplexInteraction
  - type: InputMover
  - type: Physics
    bodyType: KinematicController

- type: entity
  id: FoldableDragDropTestEntity
  components:
  - type: Item
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
  - type: Foldable
  - type: DeployFoldable
";

    /// <summary>
    /// Verifies that an unfolded DeployFoldable entity can be drag-dropped onto the user (self)
    /// by checking CanDropTargetEvent on the HandsComponent.
    /// </summary>
    [Test]
    public async Task CanDropFoldableOntoSelfTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entityManager.SpawnEntity("FoldableDragDropTestUser", mapData.GridCoords);
            var foldable = entityManager.SpawnEntity("FoldableDragDropTestEntity", mapData.GridCoords);

            // Unfolded foldable dragged onto self -> should be accepted
            var canDrop = new CanDropTargetEvent(user, foldable);
            entityManager.EventBus.RaiseLocalEvent(user, ref canDrop);

            Assert.That(canDrop.Handled, Is.True, "CanDropTargetEvent should be handled");
            Assert.That(canDrop.CanDrop, Is.True, "Should allow dropping foldable onto self");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that an already-folded entity cannot be drag-dropped onto self
    /// (it's already folded, nothing to do).
    /// </summary>
    [Test]
    public async Task CannotDropAlreadyFoldedOntoSelfTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var foldableSystem = entityManager.System<FoldableSystem>();

            var user = entityManager.SpawnEntity("FoldableDragDropTestUser", mapData.GridCoords);
            var foldable = entityManager.SpawnEntity("FoldableDragDropTestEntity", mapData.GridCoords);

            // Pre-fold the entity
            var foldComp = entityManager.GetComponent<FoldableComponent>(foldable);
            foldableSystem.TrySetFolded(foldable, foldComp, true);
            Assert.That(foldComp.IsFolded, Is.True);

            // Already folded -> should not be handled
            var canDrop = new CanDropTargetEvent(user, foldable);
            entityManager.EventBus.RaiseLocalEvent(user, ref canDrop);

            Assert.That(canDrop.CanDrop, Is.False, "Should not allow dropping already-folded entity onto self");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that a non-foldable entity dragged onto self is not handled
    /// by the DeployFoldable drag-drop handler.
    /// </summary>
    [Test]
    public async Task CannotDropNonFoldableOntoSelfTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entityManager.SpawnEntity("FoldableDragDropTestUser", mapData.GridCoords);
            // Spawn something without DeployFoldable/Foldable
            var nonFoldable = entityManager.SpawnEntity("FoldableDragDropTestUser", mapData.GridCoords);

            var canDrop = new CanDropTargetEvent(user, nonFoldable);
            entityManager.EventBus.RaiseLocalEvent(user, ref canDrop);

            Assert.That(canDrop.CanDrop, Is.False, "Should not handle non-foldable entities");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that dropping an unfolded DeployFoldable onto self actually folds it.
    /// </summary>
    [Test]
    public async Task DropFoldableOntoSelfFoldsEntityTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entityManager.SpawnEntity("FoldableDragDropTestUser", mapData.GridCoords);
            var foldable = entityManager.SpawnEntity("FoldableDragDropTestEntity", mapData.GridCoords);

            var foldComp = entityManager.GetComponent<FoldableComponent>(foldable);
            Assert.That(foldComp.IsFolded, Is.False, "Should start unfolded");

            // Raise the actual drop event
            var dropEvent = new DragDropTargetEvent(user, foldable);
            entityManager.EventBus.RaiseLocalEvent(user, ref dropEvent);

            Assert.That(dropEvent.Handled, Is.True, "DragDropTargetEvent should be handled");
            Assert.That(foldComp.IsFolded, Is.True, "Entity should be folded after drag-drop onto self");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that dragging onto a different user (not self) does not trigger the fold handler.
    /// The HONK handler explicitly checks args.User != uid.
    /// </summary>
    [Test]
    public async Task CannotDropOntoOtherPlayerTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entityManager.SpawnEntity("FoldableDragDropTestUser", mapData.GridCoords);
            var otherUser = entityManager.SpawnEntity("FoldableDragDropTestUser", mapData.GridCoords);
            var foldable = entityManager.SpawnEntity("FoldableDragDropTestEntity", mapData.GridCoords);

            // Drag foldable onto otherUser, but the event comes from user
            var canDrop = new CanDropTargetEvent(user, foldable);
            entityManager.EventBus.RaiseLocalEvent(otherUser, ref canDrop);

            Assert.That(canDrop.CanDrop, Is.False, "Should not allow dropping onto a different player");
        });

        await pair.CleanReturnAsync();
    }
}

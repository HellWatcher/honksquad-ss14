using Content.Shared.Foldable;
using Content.Shared.Friction;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.RussStation.Surgery.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Surgery;

[TestFixture]
[TestOf(typeof(SurgicalTraySystem))]
public sealed class SurgicalTraySystemTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SurgicalTrayTest
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
  - type: TileFrictionModifier
    modifier: 0.4
  - type: Foldable
  - type: SurgicalTray
    foldedFriction: 0.8
    unfoldedFriction: 0.4
";

    /// <summary>
    /// Verifies that folding a surgical tray sets friction to the folded value (higher, resists sliding)
    /// and unfolding sets it back to the unfolded value (lower, wheeled cart).
    /// </summary>
    [Test]
    public async Task FoldTogglesFrictionTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var foldableSystem = entityManager.System<FoldableSystem>();

            var tray = entityManager.SpawnEntity("SurgicalTrayTest", mapData.GridCoords);
            var friction = entityManager.GetComponent<TileFrictionModifierComponent>(tray);
            var foldable = entityManager.GetComponent<FoldableComponent>(tray);

            // Starts unfolded with unfolded friction
            Assert.That(foldable.IsFolded, Is.False);
            Assert.That(friction.Modifier, Is.EqualTo(0.4f).Within(0.001f), "Unfolded friction");

            // Fold -> friction should increase to folded value
            Assert.That(foldableSystem.TrySetFolded(tray, foldable, true), Is.True);
            Assert.That(friction.Modifier, Is.EqualTo(0.8f).Within(0.001f), "Folded friction");

            // Unfold -> friction should return to unfolded value
            Assert.That(foldableSystem.TrySetFolded(tray, foldable, false), Is.True);
            Assert.That(friction.Modifier, Is.EqualTo(0.4f).Within(0.001f), "Unfolded friction restored");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verifies that the SurgicalTrayComponent default values match the expected friction constants.
    /// </summary>
    [Test]
    public async Task DefaultFrictionValuesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var tray = entityManager.SpawnEntity("SurgicalTrayTest", mapData.GridCoords);
            var comp = entityManager.GetComponent<SurgicalTrayComponent>(tray);

            Assert.That(comp.FoldedFriction, Is.EqualTo(0.8f));
            Assert.That(comp.UnfoldedFriction, Is.EqualTo(0.4f));
        });

        await pair.CleanReturnAsync();
    }
}

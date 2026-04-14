using Content.IntegrationTests.Fixtures;
using Content.Server.RussStation.Surgery;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Interaction;
using Content.Shared.RussStation.Surgery.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Surgery;

[TestOf(typeof(SurgerySystem))]
public sealed class OrganInsertionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OrganTestPatient
  components:
  - type: Body

- type: entity
  id: OrganTestSurgeon
  components: []

# Organ with a category -- standard case
- type: entity
  id: OrganTestHeart
  components:
  - type: Organ
    category: Heart

# Organ with no category -- the null-category case this fix covers
- type: entity
  id: OrganTestImplantA
  components:
  - type: Organ

# Different prototype, also no category
- type: entity
  id: OrganTestImplantB
  components:
  - type: Organ
";

    /// <summary>
    /// Verifies that inserting two organs of the same prototype (null category)
    /// is blocked. This is the bug that was fixed: previously null-category organs
    /// bypassed the uniqueness check entirely.
    /// </summary>
    [Test]
    public async Task NullCategoryDuplicateBlockedTest()
    {
        var server = Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.SpawnEntity("OrganTestPatient", mapData.GridCoords);
            var surgeon = entMan.SpawnEntity("OrganTestSurgeon", mapData.GridCoords);

            // Add surgery state so the InteractUsing path reaches TryInsertOrgan.
            entMan.AddComponent<SurgeryDrapedComponent>(patient);
            entMan.AddComponent<ActiveSurgeryComponent>(patient);

            var body = entMan.GetComponent<BodyComponent>(patient);
            Assert.That(body.Organs, Is.Not.Null, "Body should have an organs container after init.");

            // First null-category organ: should insert.
            var implant1 = entMan.SpawnEntity("OrganTestImplantA", mapData.GridCoords);
            RaiseInteract(entMan, surgeon, implant1, patient, mapData.GridCoords);
            Assert.That(body.Organs!.ContainedEntities, Does.Contain(implant1),
                "First null-category organ should be inserted.");

            // Second organ of the same prototype: should be blocked.
            var implant2 = entMan.SpawnEntity("OrganTestImplantA", mapData.GridCoords);
            RaiseInteract(entMan, surgeon, implant2, patient, mapData.GridCoords);
            Assert.That(body.Organs.ContainedEntities, Does.Not.Contain(implant2),
                "Duplicate null-category organ of same prototype should be blocked.");
        });
    }

    /// <summary>
    /// Verifies that two different prototypes with null category can both be inserted.
    /// The uniqueness check uses prototype ID as fallback, so different prototypes are fine.
    /// </summary>
    [Test]
    public async Task DifferentNullCategoryPrototypesAllowedTest()
    {
        var server = Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.SpawnEntity("OrganTestPatient", mapData.GridCoords);
            var surgeon = entMan.SpawnEntity("OrganTestSurgeon", mapData.GridCoords);

            entMan.AddComponent<SurgeryDrapedComponent>(patient);
            entMan.AddComponent<ActiveSurgeryComponent>(patient);

            var body = entMan.GetComponent<BodyComponent>(patient);

            var implantA = entMan.SpawnEntity("OrganTestImplantA", mapData.GridCoords);
            RaiseInteract(entMan, surgeon, implantA, patient, mapData.GridCoords);
            Assert.That(body.Organs!.ContainedEntities, Does.Contain(implantA));

            // Different prototype, also null category -- should succeed.
            var implantB = entMan.SpawnEntity("OrganTestImplantB", mapData.GridCoords);
            RaiseInteract(entMan, surgeon, implantB, patient, mapData.GridCoords);
            Assert.That(body.Organs.ContainedEntities, Does.Contain(implantB),
                "Different null-category prototypes should both be allowed.");
        });
    }

    /// <summary>
    /// Verifies that the existing category-based uniqueness check still works.
    /// Two organs with the same category should be blocked.
    /// </summary>
    [Test]
    public async Task SameCategoryDuplicateBlockedTest()
    {
        var server = Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.SpawnEntity("OrganTestPatient", mapData.GridCoords);
            var surgeon = entMan.SpawnEntity("OrganTestSurgeon", mapData.GridCoords);

            entMan.AddComponent<SurgeryDrapedComponent>(patient);
            entMan.AddComponent<ActiveSurgeryComponent>(patient);

            var body = entMan.GetComponent<BodyComponent>(patient);

            var heart1 = entMan.SpawnEntity("OrganTestHeart", mapData.GridCoords);
            RaiseInteract(entMan, surgeon, heart1, patient, mapData.GridCoords);
            Assert.That(body.Organs!.ContainedEntities, Does.Contain(heart1),
                "First heart should be inserted.");

            var heart2 = entMan.SpawnEntity("OrganTestHeart", mapData.GridCoords);
            RaiseInteract(entMan, surgeon, heart2, patient, mapData.GridCoords);
            Assert.That(body.Organs.ContainedEntities, Does.Not.Contain(heart2),
                "Second heart (same category) should be blocked.");
        });
    }

    private static void RaiseInteract(
        IEntityManager entMan,
        EntityUid user,
        EntityUid used,
        EntityUid target,
        EntityCoordinates coords)
    {
        var ev = new AfterInteractUsingEvent(user, used, target, coords, canReach: true);
        entMan.EventBus.RaiseLocalEvent(target, ev);
    }
}

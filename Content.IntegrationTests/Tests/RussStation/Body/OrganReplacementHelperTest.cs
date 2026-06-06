using System.Linq;
using Content.Shared.Body;
using Content.Shared.RussStation.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(OrganReplacementHelper))]
public sealed class OrganReplacementHelperTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OrganReplaceTestHeart
  components:
  - type: Organ
    category: Heart

- type: entity
  id: OrganReplaceTestLungs
  components:
  - type: Organ
    category: Lungs

- type: entity
  id: OrganReplaceTestUncategorized
  components:
  - type: Organ

- type: entity
  id: OrganReplaceTestExistingHeart
  components:
  - type: Organ
    category: Heart

- type: entity
  id: OrganReplaceTestBodyWithHeart
  components:
  - type: Body
  - type: MobState
    allowedStates:
    - Alive
    - Critical
    - Dead
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Critical
      200: Dead
  - type: Damageable
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: SolutionContainerManager
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: OrganReplaceTestExistingHeart
";

    /// <summary>
    /// The lookup matches a categorized organ already in the body, ignores categories with no organ,
    /// and never matches on a null category (uncategorized organs deduplicate by prototype, not here).
    /// </summary>
    [Test]
    public async Task TryFindOrganByCategoryTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("OrganReplaceTestBodyWithHeart", mapData.GridCoords);
            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have an organs container");

            // Source real category values off spawned organs so the test carries no prototype-id literals.
            var heartCategory = entMan.GetComponent<OrganComponent>(
                entMan.SpawnEntity("OrganReplaceTestHeart", mapData.GridCoords)).Category;
            var lungsCategory = entMan.GetComponent<OrganComponent>(
                entMan.SpawnEntity("OrganReplaceTestLungs", mapData.GridCoords)).Category;

            Assert.That(OrganReplacementHelper.TryFindOrganByCategory(entMan, bodyComp.Organs!, heartCategory, out var found),
                Is.True, "The pre-filled heart should be found by its category");
            Assert.That(entMan.GetComponent<OrganComponent>(found).Category, Is.EqualTo(heartCategory));
            Assert.That(bodyComp.Organs!.ContainedEntities.Contains(found), Is.True);

            Assert.That(OrganReplacementHelper.TryFindOrganByCategory(entMan, bodyComp.Organs, lungsCategory, out _),
                Is.False, "No lungs organ is present, so the lookup should miss");

            Assert.That(OrganReplacementHelper.TryFindOrganByCategory(entMan, bodyComp.Organs, null, out _),
                Is.False, "A null category should never match");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Replacing a categorized organ removes the old one (dropping it out of the body) and inserts the new
    /// one, leaving exactly one organ of that category. This is the rule the autosurgeon relies on.
    /// </summary>
    [Test]
    public async Task ReplaceOrganByCategoryReplacesSameCategoryTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containerSys = entMan.System<SharedContainerSystem>();
            var transformSys = entMan.System<SharedTransformSystem>();

            var body = entMan.SpawnEntity("OrganReplaceTestBodyWithHeart", mapData.GridCoords);
            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            Assert.That(bodyComp.Organs, Is.Not.Null);

            var newHeart = entMan.SpawnEntity("OrganReplaceTestHeart", mapData.GridCoords);
            var heartCategory = entMan.GetComponent<OrganComponent>(newHeart).Category;

            Assert.That(OrganReplacementHelper.TryFindOrganByCategory(entMan, bodyComp.Organs!, heartCategory, out var oldHeart),
                Is.True);

            OrganReplacementHelper.ReplaceOrganByCategory(
                entMan, containerSys, transformSys, body, bodyComp.Organs!, newHeart, heartCategory);

            Assert.That(bodyComp.Organs!.ContainedEntities.Contains(oldHeart), Is.False,
                "Old heart should be removed from the body");
            Assert.That(bodyComp.Organs.ContainedEntities.Contains(newHeart), Is.True,
                "New heart should be inserted into the body");

            var heartCount = bodyComp.Organs.ContainedEntities.Count(o =>
                entMan.TryGetComponent<OrganComponent>(o, out var organ) && organ.Category == heartCategory);
            Assert.That(heartCount, Is.EqualTo(1), "Body should have exactly one heart after replacement");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// An uncategorized organ has no slot to displace, so it is inserted without removing anything.
    /// </summary>
    [Test]
    public async Task ReplaceOrganByCategoryInsertsUncategorizedTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containerSys = entMan.System<SharedContainerSystem>();
            var transformSys = entMan.System<SharedTransformSystem>();

            var body = entMan.SpawnEntity("OrganReplaceTestBodyWithHeart", mapData.GridCoords);
            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            Assert.That(bodyComp.Organs, Is.Not.Null);

            var countBefore = bodyComp.Organs!.ContainedEntities.Count;

            var extra = entMan.SpawnEntity("OrganReplaceTestUncategorized", mapData.GridCoords);
            OrganReplacementHelper.ReplaceOrganByCategory(
                entMan, containerSys, transformSys, body, bodyComp.Organs, extra, category: null);

            Assert.That(bodyComp.Organs.ContainedEntities.Contains(extra), Is.True);
            Assert.That(bodyComp.Organs.ContainedEntities.Count, Is.EqualTo(countBefore + 1),
                "Nothing should have been displaced");
        });

        await pair.CleanReturnAsync();
    }
}

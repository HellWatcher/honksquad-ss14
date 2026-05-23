using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Emp;
using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Skillchips;

/// <summary>
/// Tests that skillchip grants apply and revert symmetrically across install, remove,
/// and brain-body transfer scenarios.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedSkillchipSystem))]
public sealed class SkillchipGrantTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: skillchip
  id: TestChipData
  name: Test Chip
  capacityCost: 1
  grants:
  - !type:CapabilityTagGrant
    tag: test_capability

- type: skillchip
  id: TestChipData2
  name: Test Chip 2
  capacityCost: 1
  grants:
  - !type:CapabilityTagGrant
    tag: test_capability_2

- type: skillchip
  id: TestChipCategoryA1
  name: Category A Chip 1
  capacityCost: 1
  category: test_category_a
  grants:
  - !type:CapabilityTagGrant
    tag: test_cat_a1

- type: skillchip
  id: TestChipCategoryA2
  name: Category A Chip 2
  capacityCost: 1
  category: test_category_a
  grants:
  - !type:CapabilityTagGrant
    tag: test_cat_a2

- type: entity
  id: TestBrain
  components:
  - type: Brain
  - type: Organ
    category: Brain
  - type: SkillchipHolder

- type: entity
  id: TestBrainSmall
  components:
  - type: Brain
  - type: Organ
    category: Brain
  - type: SkillchipHolder
    maxCapacity: 1

- type: entity
  id: TestBody
  components:
  - type: Body
";

    /// <summary>
    /// Installing a chip on a brain that is not inside a body should not crash,
    /// and the chip should be recorded on the holder.
    /// </summary>
    [Test]
    public async Task Install_WithoutBody_RecordsChipOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var brain = em.SpawnEntity("TestBrain", mapData.GridCoords);
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);

            var installed = skillchips.TryInstall((brain, holder), "TestChipData");

            Assert.That(installed, Is.True, "TryInstall should succeed with capacity available");
            Assert.That(holder.ImplantedChips, Contains.Item(new ProtoId<SkillchipPrototype>("TestChipData")),
                "Chip should be recorded in ImplantedChips");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Installing a chip then inserting the brain into a body should apply the capability tag.
    /// </summary>
    [Test]
    public async Task BrainInserted_AppliesChipGrants()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var containers = server.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var brain = em.SpawnEntity("TestBrain", mapData.GridCoords);
            var body = em.SpawnEntity("TestBody", mapData.GridCoords);
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);

            skillchips.TryInstall((brain, holder), "TestChipData");

            // Simulate surgery: insert organ into body container
            var organContainer = containers.EnsureContainer<Container>(body, BodyComponent.ContainerID);
            containers.Insert(brain, organContainer);

            Assert.That(holder.CapabilityTags, Contains.Item("test_capability"),
                "Capability tag should be active after brain is inserted into body");
            Assert.That(skillchips.HasCapability(body, "test_capability"), Is.True,
                "HasCapability should return true via the body lookup");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Removing a chip from a brain inside a body should revert the capability tag.
    /// </summary>
    [Test]
    public async Task Remove_RevertsGrantSymmetrically()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var containers = server.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var brain = em.SpawnEntity("TestBrain", mapData.GridCoords);
            var body = em.SpawnEntity("TestBody", mapData.GridCoords);
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);

            skillchips.TryInstall((brain, holder), "TestChipData");
            var organContainer = containers.EnsureContainer<Container>(body, BodyComponent.ContainerID);
            containers.Insert(brain, organContainer);

            Assert.That(holder.CapabilityTags, Contains.Item("test_capability"),
                "Tag should be active before removal");

            skillchips.TryRemove((brain, holder), "TestChipData");

            Assert.That(holder.CapabilityTags, Does.Not.Contain("test_capability"),
                "Tag should be removed after TryRemove");
            Assert.That(skillchips.HasCapability(body, "test_capability"), Is.False,
                "HasCapability should return false after chip removal");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Removing the brain from the body should revert active chip grants.
    /// </summary>
    [Test]
    public async Task BrainRemoved_RevertsGrants()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var containers = server.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var brain = em.SpawnEntity("TestBrain", mapData.GridCoords);
            var body = em.SpawnEntity("TestBody", mapData.GridCoords);
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);

            skillchips.TryInstall((brain, holder), "TestChipData");
            var organContainer = containers.EnsureContainer<Container>(body, BodyComponent.ContainerID);
            containers.Insert(brain, organContainer);

            Assert.That(holder.CapabilityTags, Contains.Item("test_capability"));

            containers.Remove(brain, organContainer);

            Assert.That(holder.CapabilityTags, Does.Not.Contain("test_capability"),
                "Tag should be reverted when brain is removed from body");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Transferring a chipped brain to a new body should carry the grants across.
    /// The old body loses the capability and the new body gains it.
    /// </summary>
    [Test]
    public async Task BrainTransfer_CarriesGrantsToNewBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var containers = server.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var brain = em.SpawnEntity("TestBrain", mapData.GridCoords);
            var bodyA = em.SpawnEntity("TestBody", mapData.GridCoords);
            var bodyB = em.SpawnEntity("TestBody", mapData.GridCoords);
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);

            skillchips.TryInstall((brain, holder), "TestChipData");

            var containerA = containers.EnsureContainer<Container>(bodyA, BodyComponent.ContainerID);
            containers.Insert(brain, containerA);
            Assert.That(skillchips.HasCapability(bodyA, "test_capability"), Is.True);

            // Transplant brain to body B
            containers.Remove(brain, containerA);
            var containerB = containers.EnsureContainer<Container>(bodyB, BodyComponent.ContainerID);
            containers.Insert(brain, containerB);

            Assert.That(skillchips.HasCapability(bodyA, "test_capability"), Is.False,
                "Old body should lose capability after brain removal");
            Assert.That(skillchips.HasCapability(bodyB, "test_capability"), Is.True,
                "New body should gain capability after brain insertion");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Installing the same chip twice should return false and not double-apply.
    /// </summary>
    [Test]
    public async Task Install_Duplicate_ReturnsFalse()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var brain = em.SpawnEntity("TestBrain", mapData.GridCoords);
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);

            Assert.That(skillchips.TryInstall((brain, holder), "TestChipData"), Is.True);
            Assert.That(skillchips.TryInstall((brain, holder), "TestChipData"), Is.False,
                "Second install of the same chip should fail");
            Assert.That(holder.ImplantedChips.Count, Is.EqualTo(1),
                "Only one entry should exist after duplicate install attempt");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Adding EmpDisabledComponent to a mob that has a chipped brain should revert grants,
    /// and removing it should restore them.
    /// </summary>
    [Test]
    public async Task Emp_RevertsAndRestoresGrants()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var containers = server.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var brain = em.SpawnEntity("TestBrain", mapData.GridCoords);
            var body = em.SpawnEntity("TestBody", mapData.GridCoords);
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);

            skillchips.TryInstall((brain, holder), "TestChipData");
            var organContainer = containers.EnsureContainer<Container>(body, BodyComponent.ContainerID);
            containers.Insert(brain, organContainer);

            Assert.That(holder.CapabilityTags, Contains.Item("test_capability"),
                "Tag should be active before EMP");

            em.AddComponent<EmpDisabledComponent>(body);

            Assert.That(holder.CapabilityTags, Does.Not.Contain("test_capability"),
                "Tag should be reverted while EMP-disabled");

            em.RemoveComponent<EmpDisabledComponent>(body);

            Assert.That(holder.CapabilityTags, Contains.Item("test_capability"),
                "Tag should be restored after EMP clears");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Installing beyond max capacity should be rejected.
    /// </summary>
    [Test]
    public async Task Install_OverCapacity_ReturnsFalse()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var brain = em.SpawnEntity("TestBrainSmall", mapData.GridCoords);
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);

            Assert.That(skillchips.TryInstall((brain, holder), "TestChipData"), Is.True,
                "First chip should install within capacity");
            Assert.That(skillchips.TryInstall((brain, holder), "TestChipData2"), Is.False,
                "Second chip should be rejected when at capacity");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Installing two chips of the same category should be rejected.
    /// Installing a chip of a different category should succeed.
    /// </summary>
    [Test]
    public async Task Install_SameCategory_ReturnsFalse()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var brain = em.SpawnEntity("TestBrain", mapData.GridCoords);
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);

            Assert.That(skillchips.TryInstall((brain, holder), "TestChipCategoryA1"), Is.True,
                "First chip in category should install");
            Assert.That(skillchips.TryInstall((brain, holder), "TestChipCategoryA2"), Is.False,
                "Second chip in same category should be rejected");
            Assert.That(skillchips.TryInstall((brain, holder), "TestChipData"), Is.True,
                "Chip with no category should still install alongside a categorized chip");
            Assert.That(holder.ImplantedChips.Count, Is.EqualTo(2));
        });

        await pair.CleanReturnAsync();
    }
}

using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Skillchips;

/// <summary>
/// Install-rejection constraint scenarios for <see cref="SharedSkillchipSystem"/>:
/// duplicate chips, capacity limits, and per-category exclusivity. Shares the
/// <c>Prototypes</c> fixture declared in <c>SkillchipGrantTest.cs</c>.
/// </summary>
public sealed partial class SkillchipGrantTest
{
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

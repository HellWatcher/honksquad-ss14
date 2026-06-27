using Content.Server.Botany;
using Content.Server.RussStation.Botany.Systems;
using Content.Shared.Slippery;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Botany;

// Covers the two SeedDataFormatter helpers that need engine services (Loc, the
// entity manager) and so can't live in the pure Content.Tests fixture. The
// chemical-amount formula is unit-tested separately there.
[TestFixture]
[TestOf(typeof(SeedDataFormatter))]
public sealed class SeedDataFormatterTest
{
    [Test]
    public async Task FormatHarvestRepeat_MapsEachEnumValue()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var sawmill = server.ResolveDependency<ILogManager>().GetSawmill("test.seed-data-formatter");

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SeedDataFormatter.FormatHarvestRepeat(HarvestType.NoRepeat, sawmill),
                    Is.EqualTo(Loc.GetString("plant-analyzer-harvest-no-repeat")));
                Assert.That(SeedDataFormatter.FormatHarvestRepeat(HarvestType.Repeat, sawmill),
                    Is.EqualTo(Loc.GetString("plant-analyzer-harvest-repeat")));
                Assert.That(SeedDataFormatter.FormatHarvestRepeat(HarvestType.SelfHarvest, sawmill),
                    Is.EqualTo(Loc.GetString("plant-analyzer-harvest-self-harvest")));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EnumerateSeedTraits_CollectsSeedFlags()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var seed = new SeedData
            {
                Seedless = true,
                Viable = false,
                Ligneous = true,
            };

            // A bare entity carries none of the component-derived traits.
            var target = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);

            var traits = SeedDataFormatter.EnumerateSeedTraits(seed, target, entityManager);

            Assert.Multiple(() =>
            {
                Assert.That(traits, Does.Contain(Loc.GetString("plant-analyzer-trait-seedless")));
                Assert.That(traits, Does.Contain(Loc.GetString("plant-analyzer-trait-unviable")));
                Assert.That(traits, Does.Contain(Loc.GetString("plant-analyzer-trait-ligneous")));
                Assert.That(traits, Does.Not.Contain(Loc.GetString("plant-analyzer-trait-slippery")));
            });

            entityManager.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EnumerateSeedTraits_IncludesSlipperyFromTargetEntity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            // A blank seed has no intrinsic traits; the slippery trait comes from
            // a component on the scanned entity, not the seed.
            var seed = new SeedData();
            var target = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            entityManager.EnsureComponent<SlipperyComponent>(target);

            var traits = SeedDataFormatter.EnumerateSeedTraits(seed, target, entityManager);

            Assert.That(traits, Does.Contain(Loc.GetString("plant-analyzer-trait-slippery")),
                "A SlipperyComponent on the scanned entity should surface the slippery trait.");

            entityManager.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }
}

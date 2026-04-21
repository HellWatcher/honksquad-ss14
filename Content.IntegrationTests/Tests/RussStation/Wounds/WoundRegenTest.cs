using Content.Server.RussStation.Wounds;
using Content.Shared.RussStation.Wounds;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Wounds;

[TestFixture]
[TestOf(typeof(WoundRegenSystem))]
public sealed class WoundRegenTest
{
    // Sentinel "well past any tick" time used where the test needs a wound
    // entry that must not decay during the test window.
    private static readonly TimeSpan FarFuture = TimeSpan.FromHours(1);


    [Test]
    public async Task DueWoundDropsOneTier()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entity = entityManager.SpawnEntity(null, mapData.GridCoords);
            var comp = entityManager.AddComponent<WoundComponent>(entity);

            comp.ActiveWounds.Add(new WoundEntry("HeatBurn", 3) { NextDecayTime = TimeSpan.Zero });

            entityManager.System<WoundRegenSystem>().DecayWounds(entity, comp);

            Assert.That(comp.ActiveWounds, Has.Count.EqualTo(1));
            Assert.That(comp.ActiveWounds[0].Tier, Is.EqualTo(2),
                "Due tier-3 wound should have decayed exactly one tier.");
            Assert.That(comp.ActiveWounds[0].NextDecayTime, Is.GreaterThan(TimeSpan.Zero),
                "Next decay time should have been rescheduled.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NotDueWoundIsUntouched()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entity = entityManager.SpawnEntity(null, mapData.GridCoords);
            var comp = entityManager.AddComponent<WoundComponent>(entity);

            comp.ActiveWounds.Add(new WoundEntry("HeatBurn", 3) { NextDecayTime = FarFuture });

            entityManager.System<WoundRegenSystem>().DecayWounds(entity, comp);

            Assert.That(comp.ActiveWounds[0].Tier, Is.EqualTo(3),
                "Wound whose NextDecayTime is in the future should not decay.");
            Assert.That(comp.ActiveWounds[0].NextDecayTime, Is.EqualTo(FarFuture));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Tier1WoundIsRemovedOnDue()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entity = entityManager.SpawnEntity(null, mapData.GridCoords);
            var comp = entityManager.AddComponent<WoundComponent>(entity);

            comp.ActiveWounds.Add(new WoundEntry("HeatBurn", 1) { NextDecayTime = TimeSpan.Zero });

            entityManager.System<WoundRegenSystem>().DecayWounds(entity, comp);

            Assert.That(comp.ActiveWounds, Is.Empty,
                "Tier-1 wound whose decay timer expired should be removed entirely.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExpiredWoundCoexistsWithHealthySameCategoryWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entity = entityManager.SpawnEntity(null, mapData.GridCoords);
            var comp = entityManager.AddComponent<WoundComponent>(entity);

            // One tier-1 Burn due to expire (will be removed) + one tier-3 Burn that stays.
            comp.ActiveWounds.Add(new WoundEntry("HeatBurn", 1) { NextDecayTime = TimeSpan.Zero });
            comp.ActiveWounds.Add(new WoundEntry("ColdBurn", 3) { NextDecayTime = FarFuture });

            entityManager.System<WoundRegenSystem>().DecayWounds(entity, comp);

            Assert.That(comp.ActiveWounds, Has.Count.EqualTo(1),
                "Expired tier-1 wound should be removed while the healthy one stays.");
            Assert.That(comp.ActiveWounds[0].WoundTypeId.Id, Is.EqualTo("ColdBurn"),
                "The surviving wound should be the one whose decay timer had not expired.");
        });

        await pair.CleanReturnAsync();
    }
}

using Content.Shared.Rejuvenate;
using Content.Shared.RussStation.Wounds;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Wounds;

[TestFixture]
[TestOf(typeof(Content.Shared.RussStation.Wounds.Systems.SharedWoundSystem))]
public sealed class WoundRejuvenateTest
{
    [Test]
    public async Task RejuvenateClearsActiveWoundsAndBleedSource()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entity = entityManager.SpawnEntity(null, mapData.GridCoords);
            var comp = entityManager.AddComponent<WoundComponent>(entity);

            comp.ActiveWounds.Add(new WoundEntry("BluntFracture", 2));
            comp.BleedSourceDamageType = "Slash";

            entityManager.EventBus.RaiseLocalEvent(entity, new RejuvenateEvent());

            Assert.That(comp.ActiveWounds, Is.Empty,
                "RejuvenateEvent should clear ActiveWounds.");
            Assert.That(comp.BleedSourceDamageType, Is.Null,
                "RejuvenateEvent should reset BleedSourceDamageType.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RejuvenateOnCleanWoundComponentIsNoOp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entity = entityManager.SpawnEntity(null, mapData.GridCoords);
            var comp = entityManager.AddComponent<WoundComponent>(entity);

            entityManager.EventBus.RaiseLocalEvent(entity, new RejuvenateEvent());

            Assert.That(comp.ActiveWounds, Is.Empty);
            Assert.That(comp.BleedSourceDamageType, Is.Null);
        });

        await pair.CleanReturnAsync();
    }
}

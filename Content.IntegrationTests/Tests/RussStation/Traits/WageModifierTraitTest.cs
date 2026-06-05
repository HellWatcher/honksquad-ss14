using Content.Server.RussStation.Traits;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Traits;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Traits;

/// <summary>
/// Verifies the wage-modifying traits hook <see cref="GetWageEvent"/> and scale the
/// payroll wage by their configured multipliers: Indebted halves it,
/// Negotiator increases it by 50%.
/// </summary>
[TestFixture]
[TestOf(typeof(IndebtedSystem))]
[TestOf(typeof(NegotiatorSystem))]
public sealed class WageModifierTraitTest
{
    [Test]
    public async Task IndebtedHalvesWageTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var comp = entMan.AddComponent<IndebtedComponent>(mob);

            var ev = new GetWageEvent(100);
            entMan.EventBus.RaiseLocalEvent(mob, ref ev);

            Assert.That(ev.Wage, Is.EqualTo((int)(100 * comp.WageMultiplier)),
                "Indebted should scale the wage by its WageMultiplier.");
            Assert.That(ev.Wage, Is.EqualTo(50), "Default Indebted multiplier of 0.5 should halve a 100 wage.");

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NegotiatorRaisesWageTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var comp = entMan.AddComponent<NegotiatorComponent>(mob);

            var ev = new GetWageEvent(100);
            entMan.EventBus.RaiseLocalEvent(mob, ref ev);

            Assert.That(ev.Wage, Is.EqualTo((int)(100 * comp.WageMultiplier)),
                "Negotiator should scale the wage by its WageMultiplier.");
            Assert.That(ev.Wage, Is.EqualTo(150), "Default Negotiator multiplier of 1.5 should raise a 100 wage to 150.");

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }
}

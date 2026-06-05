using Content.Server.RussStation.Economy;
using Content.Shared.RussStation.Economy;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

// EconomyConstants exists in both the server and shared economy namespaces; the
// SkavenPaydayMultiplier this test asserts against lives on the server one.
using EconomyConstants = Content.Server.RussStation.Economy.EconomyConstants;

namespace Content.IntegrationTests.Tests.RussStation.Economy;

/// <summary>
/// Verifies that <see cref="SkavenPaydaySystem"/> scales a Skaven's wage down to the
/// configured fraction (<see cref="EconomyConstants.SkavenPaydayMultiplier"/> = 0.25)
/// when <see cref="PayrollSystem"/> raises <see cref="GetWageEvent"/> before depositing.
/// </summary>
[TestFixture]
[TestOf(typeof(SkavenPaydaySystem))]
public sealed class SkavenPaydayTest
{
    [Test]
    public async Task SkavenWageIsQuarteredTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var skaven = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<SkavenPaydayComponent>(skaven);

            var ev = new GetWageEvent(100);
            entMan.EventBus.RaiseLocalEvent(skaven, ref ev);

            Assert.That(ev.Wage, Is.EqualTo((int)(100 * EconomyConstants.SkavenPaydayMultiplier)),
                "Skaven payday should scale the wage by the SkavenPaydayMultiplier.");
            Assert.That(ev.Wage, Is.EqualTo(25), "0.25 * 100 should deposit 25.");

            entMan.DeleteEntity(skaven);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// An entity without the component should be untouched by the wage hook.
    /// </summary>
    [Test]
    public async Task NonSkavenWageUnchangedTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var crew = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            var ev = new GetWageEvent(100);
            entMan.EventBus.RaiseLocalEvent(crew, ref ev);

            Assert.That(ev.Wage, Is.EqualTo(100), "A non-Skaven entity's wage should be unchanged.");

            entMan.DeleteEntity(crew);
        });

        await pair.CleanReturnAsync();
    }
}

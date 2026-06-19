using Content.Server.Atmos.EntitySystems;
using Content.Server.RussStation.Atmos.Reactions;
using Content.Shared.Atmos;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Atmos;

/// <summary>
/// Directly exercises <see cref="ReactionHelper.AdjustEnergy"/>, the shared energy/temperature
/// adjustment every fork reaction depends on: heating, cooling, the TCMB clamp, and heat-scale
/// division. Shares the <c>Tol</c> tolerance declared in <c>GasReactionTest.cs</c>.
/// </summary>
public sealed partial class GasReactionTest
{
    [Test]
    public async Task AdjustEnergy_PositiveEnergy_HeatsMixture()
    {
        var server = Server;
        var atmos = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<AtmosphereSystem>();

        await server.WaitAssertion(() =>
        {
            var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = 300f };
            mix.AdjustMoles(Gas.Nitrogen, 100f);
            var oldCap = atmos.GetHeatCapacity(mix, true);
            ReactionHelper.AdjustEnergy(mix, atmos, oldCap, 100_000f, 1f);
            Assert.That(mix.Temperature, Is.GreaterThan(300f));
        });
    }

    [Test]
    public async Task AdjustEnergy_NegativeEnergy_CoolsMixture()
    {
        var server = Server;
        var atmos = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<AtmosphereSystem>();

        await server.WaitAssertion(() =>
        {
            var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = 300f };
            mix.AdjustMoles(Gas.Nitrogen, 100f);
            var oldCap = atmos.GetHeatCapacity(mix, true);
            ReactionHelper.AdjustEnergy(mix, atmos, oldCap, -50_000f, 1f);
            Assert.That(mix.Temperature, Is.LessThan(300f));
        });
    }

    [Test]
    public async Task AdjustEnergy_ExtremeNegativeEnergy_ClampsToTCMB()
    {
        var server = Server;
        var atmos = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<AtmosphereSystem>();

        await server.WaitAssertion(() =>
        {
            var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = 300f };
            mix.AdjustMoles(Gas.Nitrogen, 100f);
            var oldCap = atmos.GetHeatCapacity(mix, true);
            ReactionHelper.AdjustEnergy(mix, atmos, oldCap, -1_000_000_000f, 1f);
            Assert.That(mix.Temperature, Is.EqualTo(Atmospherics.TCMB).Within(Tol));
        });
    }

    [Test]
    public async Task AdjustEnergy_HeatScaleHalves_HalvesEnergyApplied()
    {
        var server = Server;
        var atmos = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<AtmosphereSystem>();

        await server.WaitAssertion(() =>
        {
            var mix1 = new GasMixture(Atmospherics.CellVolume) { Temperature = 300f };
            mix1.AdjustMoles(Gas.Nitrogen, 100f);
            var cap1 = atmos.GetHeatCapacity(mix1, true);
            ReactionHelper.AdjustEnergy(mix1, atmos, cap1, 100_000f, 1f);

            var mix2 = new GasMixture(Atmospherics.CellVolume) { Temperature = 300f };
            mix2.AdjustMoles(Gas.Nitrogen, 100f);
            var cap2 = atmos.GetHeatCapacity(mix2, true);
            ReactionHelper.AdjustEnergy(mix2, atmos, cap2, 100_000f, 2f);

            // heatScale divides energy, so scale=2 should yield a smaller delta above 300K
            var delta1 = mix1.Temperature - 300f;
            var delta2 = mix2.Temperature - 300f;
            Assert.Multiple(() =>
            {
                Assert.That(delta1, Is.GreaterThan(0f));
                Assert.That(delta2, Is.GreaterThan(0f));
                Assert.That(delta2, Is.EqualTo(delta1 / 2f).Within(0.1));
            });
        });
    }
}

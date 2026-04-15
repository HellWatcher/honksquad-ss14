using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.RussStation.Atmos;
using Content.Server.RussStation.Atmos.Reactions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Atmos;

/// <summary>
/// Covers the fork's <see cref="IGasReactionEffect"/> implementations in
/// <c>Content.Server/RussStation/Atmos/Reactions</c>. Each reaction is
/// exercised with a happy-path input and at least one no-reaction input,
/// plus branch-specific cases where a reaction has more than one early
/// return (BZ decomposition). Also covers
/// <see cref="ReactionHelper.AdjustEnergy"/> directly because every reaction
/// depends on it.
/// </summary>
public sealed class GasReactionTest : GameTest
{
    private const double Tol = 1e-3;

    /// <summary>
    /// Builds a fresh mixture, runs the reaction, and hands the result to
    /// <paramref name="assert"/>. Keeps each test body tiny.
    /// </summary>
    private async Task TestReaction<T>(
        Action<GasMixture> setup,
        Action<ReactionResult, GasMixture, AtmosphereSystem> assert)
        where T : IGasReactionEffect, new()
    {
        var server = Server;
        var atmos = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<AtmosphereSystem>();

        await server.WaitAssertion(() =>
        {
            var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
            setup(mix);
            var result = new T().React(mix, null, atmos, 1f);
            assert(result, mix, atmos);
        });
    }

    // ---- HydrogenFire ------------------------------------------------------

    [Test]
    public Task HydrogenFire_WithH2AndO2_ProducesWaterVaporAndHeats() => TestReaction<HydrogenFireReaction>(
        mix =>
        {
            mix.Temperature = 1000f;
            mix.AdjustMoles(Gas.Hydrogen, 10f);
            mix.AdjustMoles(Gas.Oxygen, 50f);
        },
        (result, mix, _) =>
        {
            // burned = min(10/2, 50/20, 10, 100) = 2.5
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.Hydrogen), Is.EqualTo(7.5f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Oxygen), Is.EqualTo(48.75f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.WaterVapor), Is.EqualTo(2.5f).Within(Tol));
                Assert.That(mix.Temperature, Is.GreaterThan(1000f));
            });
        });

    [Test]
    public Task HydrogenFire_WithoutHydrogen_DoesNotReact() => TestReaction<HydrogenFireReaction>(
        mix =>
        {
            mix.Temperature = 1000f;
            mix.AdjustMoles(Gas.Oxygen, 50f);
        },
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.Oxygen), Is.EqualTo(50f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.WaterVapor), Is.Zero);
            });
        });

    // ---- BZFormation -------------------------------------------------------

    [Test]
    public Task BZFormation_BalancedReactants_ProducesBZ() => TestReaction<BZFormationReaction>(
        mix =>
        {
            mix.Temperature = 280f;
            mix.AdjustMoles(Gas.NitrousOxide, 5f);
            mix.AdjustMoles(Gas.Plasma, 5f);
        },
        (result, mix, _) =>
        {
            // plasma/n2o = 1 (not > 3), ratio = 1, produced = 5*0.4*1 = 2
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.NitrousOxide), Is.EqualTo(3f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Plasma), Is.EqualTo(3f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.BZ), Is.EqualTo(2f).Within(Tol));
            });
        });

    [Test]
    public Task BZFormation_HighPlasmaRatio_DecomposesN2O() => TestReaction<BZFormationReaction>(
        mix =>
        {
            mix.Temperature = 280f;
            mix.AdjustMoles(Gas.NitrousOxide, 1f);
            mix.AdjustMoles(Gas.Plasma, 10f); // plasma/n2o = 10 > 3
        },
        (result, mix, _) =>
        {
            // Full 1 mol N2O decomposes into 0.5 N2 + 0.5 O2, no BZ
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.NitrousOxide), Is.Zero.Within(Tol));
                Assert.That(mix.GetMoles(Gas.Plasma), Is.EqualTo(10f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Nitrogen), Is.EqualTo(0.5f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Oxygen), Is.EqualTo(0.5f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.BZ), Is.Zero);
            });
        });

    [Test]
    public Task BZFormation_NoN2O_DoesNotReact() => TestReaction<BZFormationReaction>(
        mix =>
        {
            mix.AdjustMoles(Gas.Plasma, 10f);
        },
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.Plasma), Is.EqualTo(10f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.BZ), Is.Zero);
            });
        });

    // ---- PluoxiumFormation -------------------------------------------------

    [Test]
    public Task PluoxiumFormation_WithAllReactants_ProducesPluoxium() => TestReaction<PluoxiumFormationReaction>(
        mix =>
        {
            mix.Temperature = 200f;
            mix.AdjustMoles(Gas.CarbonDioxide, 10f);
            mix.AdjustMoles(Gas.Oxygen, 10f);
            mix.AdjustMoles(Gas.Tritium, 1f);
        },
        (result, mix, _) =>
        {
            // produced = min(10, 20, 100, 5) = 5
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.CarbonDioxide), Is.EqualTo(5f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Oxygen), Is.EqualTo(7.5f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Tritium), Is.EqualTo(0.95f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Pluoxium), Is.EqualTo(5f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Hydrogen), Is.EqualTo(0.05f).Within(Tol));
            });
        });

    [Test]
    public Task PluoxiumFormation_NoCarbonDioxide_DoesNotReact() => TestReaction<PluoxiumFormationReaction>(
        mix =>
        {
            mix.Temperature = 200f;
            mix.AdjustMoles(Gas.Oxygen, 10f);
            mix.AdjustMoles(Gas.Tritium, 1f);
        },
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.Pluoxium), Is.Zero);
            });
        });

    // ---- HalonOxygenRemoval ------------------------------------------------

    [Test]
    public Task HalonOxygenRemoval_HotOxygenRichAir_ConvertsO2ToCO2AndCools() => TestReaction<HalonOxygenRemovalReaction>(
        mix =>
        {
            mix.Temperature = 4000f;
            mix.AdjustMoles(Gas.Halon, 1f);
            mix.AdjustMoles(Gas.Oxygen, 40f);
        },
        (result, mix, _) =>
        {
            // heatEfficiency = min(4000/3731.5, min(1, 40/20)) = min(1.072, 1) = 1
            // consumes 1 halon, 20 O2, produces 20 CO2
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.Halon), Is.Zero.Within(Tol));
                Assert.That(mix.GetMoles(Gas.Oxygen), Is.EqualTo(20f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.CarbonDioxide), Is.EqualTo(20f).Within(Tol));
                Assert.That(mix.Temperature, Is.LessThan(4000f));
            });
        });

    [Test]
    public Task HalonOxygenRemoval_NoHalon_DoesNotReact() => TestReaction<HalonOxygenRemovalReaction>(
        mix =>
        {
            mix.Temperature = 4000f;
            mix.AdjustMoles(Gas.Oxygen, 40f);
        },
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.Oxygen), Is.EqualTo(40f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.CarbonDioxide), Is.Zero);
            });
        });

    // ---- HealiumFormation --------------------------------------------------

    [Test]
    public Task HealiumFormation_FrezonAndBZ_ProducesHealiumAndHeats() => TestReaction<HealiumFormationReaction>(
        mix =>
        {
            mix.Temperature = 100f;
            mix.AdjustMoles(Gas.Frezon, 200f);
            mix.AdjustMoles(Gas.BZ, 200f);
        },
        (result, mix, _) =>
        {
            // heatEfficiency = min(100*0.3=30, min(200/2.75=72.7, 200/0.25=800)) = 30
            // consumes 82.5 frezon + 7.5 bz, produces 90 healium
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.Frezon), Is.EqualTo(117.5f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.BZ), Is.EqualTo(192.5f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Healium), Is.EqualTo(90f).Within(Tol));
                Assert.That(mix.Temperature, Is.GreaterThan(100f));
            });
        });

    [Test]
    public Task HealiumFormation_NoFrezon_DoesNotReact() => TestReaction<HealiumFormationReaction>(
        mix =>
        {
            mix.Temperature = 100f;
            mix.AdjustMoles(Gas.BZ, 200f);
        },
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.BZ), Is.EqualTo(200f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Healium), Is.Zero);
            });
        });

    // ---- NitriumFormation --------------------------------------------------

    [Test]
    public Task NitriumFormation_HotTritiumNitrogenBZ_ProducesNitriumAndCools() => TestReaction<NitriumFormationReaction>(
        mix =>
        {
            mix.Temperature = 6000f;
            mix.AdjustMoles(Gas.Tritium, 5f);
            mix.AdjustMoles(Gas.Nitrogen, 5f);
            mix.AdjustMoles(Gas.BZ, 1f);
        },
        (result, mix, _) =>
        {
            // NitriumFormationTempDivisor = FireMin*8 = 2985.2
            // heatEff = min(6000/2985.2≈2.0098, min(5, min(5, 1/0.05=20))) = 2.0098
            var expected = 6000f / RussAtmospherics.NitriumFormationTempDivisor;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.Tritium), Is.EqualTo(5f - expected).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Nitrogen), Is.EqualTo(5f - expected).Within(Tol));
                Assert.That(mix.GetMoles(Gas.BZ), Is.EqualTo(1f - expected * 0.05f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Nitrium), Is.EqualTo(expected).Within(Tol));
                Assert.That(mix.Temperature, Is.LessThan(6000f));
            });
        });

    [Test]
    public Task NitriumFormation_NoTritium_DoesNotReact() => TestReaction<NitriumFormationReaction>(
        mix =>
        {
            mix.Temperature = 6000f;
            mix.AdjustMoles(Gas.Nitrogen, 5f);
            mix.AdjustMoles(Gas.BZ, 1f);
        },
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.Nitrium), Is.Zero);
            });
        });

    // ---- NitriumDecomposition ----------------------------------------------

    [Test]
    public Task NitriumDecomposition_WithNitrium_ProducesH2AndN2AndHeats() => TestReaction<NitriumDecompositionReaction>(
        mix =>
        {
            mix.Temperature = 300f;
            mix.AdjustMoles(Gas.Nitrium, 10f);
        },
        (result, mix, _) =>
        {
            // heatEff = min(300/2985.2≈0.1005, 10) = 0.1005
            var expected = 300f / RussAtmospherics.NitriumDecompositionTempDivisor;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.Nitrium), Is.EqualTo(10f - expected).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Hydrogen), Is.EqualTo(expected).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Nitrogen), Is.EqualTo(expected).Within(Tol));
                Assert.That(mix.Temperature, Is.GreaterThan(300f));
            });
        });

    [Test]
    public Task NitriumDecomposition_NoNitrium_DoesNotReact() => TestReaction<NitriumDecompositionReaction>(
        mix => mix.Temperature = 300f,
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.Hydrogen), Is.Zero);
                Assert.That(mix.GetMoles(Gas.Nitrogen), Is.Zero);
            });
        });

    // ---- ProtoNitrateFormation ---------------------------------------------

    [Test]
    public Task ProtoNitrateFormation_PluoxiumAndHydrogenHot_ProducesProtoNitrate() => TestReaction<ProtoNitrateFormationReaction>(
        mix =>
        {
            mix.Temperature = 6000f;
            mix.AdjustMoles(Gas.Pluoxium, 10f);
            mix.AdjustMoles(Gas.Hydrogen, 20f);
        },
        (result, mix, _) =>
        {
            // heatEff = min(6000*0.005=30, min(10/0.2=50, 20/2=10)) = 10
            // consumes 2 pluoxium + 20 hydrogen, produces 22 proto-nitrate.
            // The reaction adds positive energy, but the product has much
            // more heat capacity than the reactants so the final temperature
            // is a function of both; only assert moles here.
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.Pluoxium), Is.EqualTo(8f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Hydrogen), Is.Zero.Within(Tol));
                Assert.That(mix.GetMoles(Gas.ProtoNitrate), Is.EqualTo(22f).Within(Tol));
            });
        });

    [Test]
    public Task ProtoNitrateFormation_NoPluoxium_DoesNotReact() => TestReaction<ProtoNitrateFormationReaction>(
        mix =>
        {
            mix.Temperature = 6000f;
            mix.AdjustMoles(Gas.Hydrogen, 20f);
        },
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.ProtoNitrate), Is.Zero);
            });
        });

    // ---- ProtoNitrateHydrogen ----------------------------------------------

    [Test]
    public Task ProtoNitrateHydrogen_WithHydrogen_ConvertsToMoreProtoNitrateAndCools() => TestReaction<ProtoNitrateHydrogenReaction>(
        mix =>
        {
            mix.Temperature = 400f;
            mix.AdjustMoles(Gas.Hydrogen, 10f);
            mix.AdjustMoles(Gas.ProtoNitrate, 10f);
        },
        (result, mix, _) =>
        {
            // produced = min(5, min(10, 10)) = 5
            // consumes 5 H2, adds 2.5 proto-nitrate
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.Hydrogen), Is.EqualTo(5f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.ProtoNitrate), Is.EqualTo(12.5f).Within(Tol));
                Assert.That(mix.Temperature, Is.LessThan(400f));
            });
        });

    [Test]
    public Task ProtoNitrateHydrogen_NoHydrogen_DoesNotReact() => TestReaction<ProtoNitrateHydrogenReaction>(
        mix =>
        {
            mix.Temperature = 400f;
            mix.AdjustMoles(Gas.ProtoNitrate, 10f);
        },
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.ProtoNitrate), Is.EqualTo(10f).Within(Tol));
            });
        });

    // ---- ProtoNitrateTritium -----------------------------------------------

    [Test]
    public Task ProtoNitrateTritium_WithTritium_ConvertsToHydrogenAndHeats() => TestReaction<ProtoNitrateTritiumReaction>(
        mix =>
        {
            mix.Temperature = 300f;
            mix.AdjustMoles(Gas.Tritium, 20f);
            mix.AdjustMoles(Gas.ProtoNitrate, 10f);
        },
        (result, mix, _) =>
        {
            // first = 300/34 * (20*10)/(20+10*10) = 8.824 * 1.667 = 14.706
            // second = min(20, 10/0.01=1000) = 20
            // produced = min(14.706, 20) = 14.706
            const float produced = 300f / 34f * (20f * 10f) / (20f + 10f * 10f);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.Tritium), Is.EqualTo(20f - produced).Within(Tol));
                Assert.That(mix.GetMoles(Gas.ProtoNitrate), Is.EqualTo(10f - produced * 0.01f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Hydrogen), Is.EqualTo(produced).Within(Tol));
                Assert.That(mix.Temperature, Is.GreaterThan(300f));
            });
        });

    [Test]
    public Task ProtoNitrateTritium_NoTritium_DoesNotReact() => TestReaction<ProtoNitrateTritiumReaction>(
        mix =>
        {
            mix.Temperature = 300f;
            mix.AdjustMoles(Gas.ProtoNitrate, 10f);
        },
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.Hydrogen), Is.Zero);
            });
        });

    // ---- ProtoNitrateBZ ----------------------------------------------------

    [Test]
    public Task ProtoNitrateBZ_WithBZ_DecomposesIntoN2HeAndPlasma() => TestReaction<ProtoNitrateBZReaction>(
        mix =>
        {
            mix.Temperature = 270f;
            mix.AdjustMoles(Gas.BZ, 10f);
            mix.AdjustMoles(Gas.ProtoNitrate, 10f);
        },
        (result, mix, _) =>
        {
            // consumed = min(270/2240 * 100/20, min(10,10)) = min(0.6027, 10) = 0.6027
            const float consumed = 270f / 2240f * (10f * 10f) / (10f + 10f);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.Reacting));
                Assert.That(mix.GetMoles(Gas.BZ), Is.EqualTo(10f - consumed).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Nitrogen), Is.EqualTo(consumed * 0.4f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Helium), Is.EqualTo(consumed * 1.6f).Within(Tol));
                Assert.That(mix.GetMoles(Gas.Plasma), Is.EqualTo(consumed * 0.8f).Within(Tol));
                Assert.That(mix.Temperature, Is.GreaterThan(270f));
            });
        });

    [Test]
    public Task ProtoNitrateBZ_NoBZ_DoesNotReact() => TestReaction<ProtoNitrateBZReaction>(
        mix =>
        {
            mix.Temperature = 270f;
            mix.AdjustMoles(Gas.ProtoNitrate, 10f);
        },
        (result, mix, _) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ReactionResult.NoReaction));
                Assert.That(mix.GetMoles(Gas.ProtoNitrate), Is.EqualTo(10f).Within(Tol));
            });
        });

    // ---- ReactionHelper.AdjustEnergy --------------------------------------

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

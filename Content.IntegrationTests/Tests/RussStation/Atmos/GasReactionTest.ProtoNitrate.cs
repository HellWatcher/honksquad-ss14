using Content.Server.RussStation.Atmos.Reactions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;

namespace Content.IntegrationTests.Tests.RussStation.Atmos;

/// <summary>
/// The proto-nitrate gas chain: formation from pluoxium/hydrogen and the three
/// proto-nitrate conversions (hydrogen, tritium, BZ). Uses the
/// <c>TestReaction&lt;T&gt;</c> helper declared in <c>GasReactionTest.cs</c>.
/// </summary>
public sealed partial class GasReactionTest
{
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
}

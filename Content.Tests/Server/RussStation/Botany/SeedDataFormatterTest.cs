using Content.Server.Botany;
using Content.Server.RussStation.Botany.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Server.RussStation.Botany;

[TestFixture, TestOf(typeof(SeedDataFormatter))]
[Parallelizable(ParallelScope.All)]
public sealed class SeedDataFormatterTest
{
    // Mirrors the worked example documented on SeedChemQuantity.PotencyDivisor:
    // a divisor of 20 with potency 55 gives 2.75, added on top of the Min of 1
    // for a final 3.75.
    [Test]
    public void CalculateChemicalAmount_AddsPotencyBonusOnTopOfMin()
    {
        var quantity = new SeedChemQuantity
        {
            Min = FixedPoint2.New(1),
            Max = FixedPoint2.New(100),
            PotencyDivisor = 20f,
        };

        var amount = SeedDataFormatter.CalculateChemicalAmount(quantity, 55f);

        Assert.That(amount, Is.EqualTo(FixedPoint2.New(3.75f)));
    }

    [Test]
    public void CalculateChemicalAmount_ClampsToMax()
    {
        var quantity = new SeedChemQuantity
        {
            Min = FixedPoint2.New(1),
            Max = FixedPoint2.New(2),
            PotencyDivisor = 20f,
        };

        // Min + 55/20 would be 3.75, but Max caps the result at 2.
        var amount = SeedDataFormatter.CalculateChemicalAmount(quantity, 55f);

        Assert.That(amount, Is.EqualTo(FixedPoint2.New(2)));
    }

    [Test]
    public void CalculateChemicalAmount_ZeroDivisorYieldsMinOnly()
    {
        var quantity = new SeedChemQuantity
        {
            Min = FixedPoint2.New(5),
            Max = FixedPoint2.New(100),
            PotencyDivisor = 0f,
        };

        // A divisor of 0 disables the potency bonus, regardless of potency.
        var amount = SeedDataFormatter.CalculateChemicalAmount(quantity, 55f);

        Assert.That(amount, Is.EqualTo(FixedPoint2.New(5)));
    }

    [Test]
    public void CalculateChemicalAmount_ZeroPotencyYieldsMinOnly()
    {
        var quantity = new SeedChemQuantity
        {
            Min = FixedPoint2.New(5),
            Max = FixedPoint2.New(100),
            PotencyDivisor = 20f,
        };

        // Zero potency means no bonus even when a divisor is set.
        var amount = SeedDataFormatter.CalculateChemicalAmount(quantity, 0f);

        Assert.That(amount, Is.EqualTo(FixedPoint2.New(5)));
    }
}

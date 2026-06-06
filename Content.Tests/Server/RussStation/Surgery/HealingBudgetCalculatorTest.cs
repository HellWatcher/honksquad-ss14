using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.RussStation.Surgery;
using NUnit.Framework;

namespace Content.Tests.Server.RussStation.Surgery;

[TestFixture, TestOf(typeof(HealingBudgetCalculator))]
[Parallelizable(ParallelScope.All)]
public sealed class HealingBudgetCalculatorTest
{
    private static DamageSpecifier Damage(params (string Type, float Amount)[] entries)
    {
        var spec = new DamageSpecifier();
        foreach (var (type, amount) in entries)
            spec.DamageDict[type] = FixedPoint2.New(amount);
        return spec;
    }

    /// <summary>
    /// budget = flat + total*multiplier, distributed proportionally across the eligible types
    /// in proportion to each type's share of the current damage.
    /// </summary>
    [Test]
    public void Calculate_DistributesBudgetProportionally()
    {
        var current = Damage(("Blunt", 80f), ("Slash", 20f));
        var eligible = Damage(("Blunt", 1f), ("Slash", 1f)); // only the keys are read

        var result = HealingBudgetCalculator.Calculate(current, eligible, flat: 5f, multiplier: 0.07f);

        // total = 100, budget = 5 + 100*0.07 = 12 -> Blunt 12*0.8 = 9.6, Slash 12*0.2 = 2.4
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.DamageDict["Blunt"].Float(), Is.EqualTo(-9.6f).Within(0.001f));
        Assert.That(result.DamageDict["Slash"].Float(), Is.EqualTo(-2.4f).Within(0.001f));
    }

    /// <summary>All produced entries heal (are negative), never aggravate.</summary>
    [Test]
    public void Calculate_ProducesOnlyNegativeAmounts()
    {
        var current = Damage(("Blunt", 50f), ("Slash", 50f));
        var eligible = Damage(("Blunt", 1f), ("Slash", 1f));

        var result = HealingBudgetCalculator.Calculate(current, eligible, flat: 10f, multiplier: 0.1f);

        Assert.That(result, Is.Not.Null);
        foreach (var amount in result!.DamageDict.Values)
            Assert.That(amount, Is.LessThan(FixedPoint2.Zero));
    }

    /// <summary>A flat-only budget (multiplier 0) heals exactly the flat amount on one type.</summary>
    [Test]
    public void Calculate_FlatOnly_HealsFlatAmount()
    {
        var current = Damage(("Blunt", 40f));
        var eligible = Damage(("Blunt", 1f));

        var result = HealingBudgetCalculator.Calculate(current, eligible, flat: 10f, multiplier: 0f);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.DamageDict["Blunt"].Float(), Is.EqualTo(-10f).Within(0.001f));
    }

    /// <summary>A multiplier-only budget (flat 0) scales with current eligible damage.</summary>
    [Test]
    public void Calculate_MultiplierOnly_ScalesWithDamage()
    {
        var current = Damage(("Blunt", 100f));
        var eligible = Damage(("Blunt", 1f));

        var result = HealingBudgetCalculator.Calculate(current, eligible, flat: 0f, multiplier: 0.07f);

        // budget = 0 + 100*0.07 = 7
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.DamageDict["Blunt"].Float(), Is.EqualTo(-7f).Within(0.001f));
    }

    /// <summary>Eligible types with no current damage are skipped, not zero-healed.</summary>
    [Test]
    public void Calculate_SkipsTypesWithNoCurrentDamage()
    {
        var current = Damage(("Blunt", 50f)); // no Slash present
        var eligible = Damage(("Blunt", 1f), ("Slash", 1f));

        var result = HealingBudgetCalculator.Calculate(current, eligible, flat: 5f, multiplier: 0.07f);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.DamageDict, Has.Count.EqualTo(1));
        Assert.That(result.DamageDict.ContainsKey("Slash"), Is.False);
        // budget = 5 + 50*0.07 = 8.5, all to Blunt
        Assert.That(result.DamageDict["Blunt"].Float(), Is.EqualTo(-8.5f).Within(0.001f));
    }

    /// <summary>No eligible damage present (only a non-eligible type) yields no heal at all.</summary>
    [Test]
    public void Calculate_NoEligibleDamage_ReturnsNull()
    {
        var current = Damage(("Slash", 10f));
        var eligible = Damage(("Blunt", 1f));

        var result = HealingBudgetCalculator.Calculate(current, eligible, flat: 5f, multiplier: 0.07f);

        Assert.That(result, Is.Null);
    }

    /// <summary>An empty current-damage set yields no heal.</summary>
    [Test]
    public void Calculate_NoDamageAtAll_ReturnsNull()
    {
        var current = new DamageSpecifier();
        var eligible = Damage(("Blunt", 1f));

        var result = HealingBudgetCalculator.Calculate(current, eligible, flat: 5f, multiplier: 0.07f);

        Assert.That(result, Is.Null);
    }
}

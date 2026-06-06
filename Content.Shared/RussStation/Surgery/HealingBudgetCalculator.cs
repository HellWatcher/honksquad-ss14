using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared.RussStation.Surgery;

/// <summary>
/// Computes a "flat + (eligible damage × multiplier), distributed proportionally" healing budget
/// and turns it into a negative <see cref="DamageSpecifier"/> ready to feed into the damageable
/// system. Pure and standalone so implants, potions, and surgery tend-steps can all share the one
/// formula instead of re-deriving it.
/// </summary>
public static class HealingBudgetCalculator
{
    /// <summary>
    /// Builds the heal specifier for a tend-style effect.
    /// </summary>
    /// <param name="currentDamage">The patient's current (positive) damage by type.</param>
    /// <param name="eligible">Damage types this effect is allowed to heal; only its keys are read.</param>
    /// <param name="flat">Flat healing budget baseline.</param>
    /// <param name="multiplier">Fraction of current eligible damage added to the budget.</param>
    /// <returns>
    /// A negative <see cref="DamageSpecifier"/> distributing the budget across the eligible damage
    /// proportionally, or <c>null</c> when there is no eligible damage to heal.
    /// </returns>
    public static DamageSpecifier? Calculate(
        DamageSpecifier currentDamage,
        DamageSpecifier eligible,
        float flat,
        float multiplier)
    {
        var totalDamage = FixedPoint2.Zero;
        foreach (var type in eligible.DamageDict.Keys)
        {
            if (currentDamage.DamageDict.TryGetValue(type, out var current))
                totalDamage += current;
        }

        if (totalDamage <= 0)
            return null;

        var budget = FixedPoint2.New(flat + (float) totalDamage * multiplier);

        // Distribute proportionally across eligible damage types.
        var healSpec = new DamageSpecifier();
        foreach (var type in eligible.DamageDict.Keys)
        {
            if (currentDamage.DamageDict.TryGetValue(type, out var current) && current > 0)
            {
                var share = budget * current / totalDamage;
                healSpec.DamageDict[type] = -share;
            }
        }

        return healSpec;
    }
}

using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Effects;
using Content.Shared.RussStation.Wounds;

namespace Content.Server.RussStation.Surgery;

public sealed partial class SurgerySystem
{
    /// <summary>
    /// Gate-keeps a procedure-menu selection. The entities are assumed already resolved; this checks
    /// the surgeon/patient/drape state and that the procedure has something to do.
    /// </summary>
    /// <returns>
    /// <c>Valid</c> is false when the selection should be rejected. <c>Error</c> is the localized
    /// popup to show the surgeon, or <c>null</c> for a silent rejection (out of range / self-surgery).
    /// </returns>
    private (bool Valid, string? Error) ValidateProcedureSelection(
        EntityUid surgeon,
        EntityUid target,
        EntityUid bedsheet,
        SurgeryProcedurePrototype proto)
    {
        // Surgeon must be in range of patient.
        if (!_interaction.InRangeUnobstructed(surgeon, target))
            return (false, null);

        // No self-surgery.
        if (surgeon == target)
            return (false, null);

        // Patient must not already be draped.
        if (_drapedQuery.HasComp(target))
            return (false, Loc.GetString("surgery-already-draped"));

        // Patient must still be down.
        if (!_standing.IsDown(target))
            return (false, Loc.GetString("surgery-patient-not-down"));

        // Drape item must still exist and have Draping quality.
        if (!Exists(bedsheet) || !_tool.HasQuality(bedsheet, DrapingQuality))
            return (false, Loc.GetString("surgery-drape-missing"));

        // If the procedure only heals and the patient has no matching damage, refuse.
        if (!ProcedureHasAnythingToTend(target, proto))
            return (false, Loc.GetString("surgery-nothing-to-tend", ("target", target)));

        return (true, null);
    }

    /// <summary>
    /// True if the procedure has at least one useful step whose target condition
    /// is present on the patient: a healing step matching current damage above a
    /// small epsilon, or a wound-clearing step matching an active wound category.
    /// Procedures with no healing or wound-clearing steps (e.g. organ manipulation)
    /// always pass.
    /// </summary>
    private bool ProcedureHasAnythingToTend(EntityUid patient, SurgeryProcedurePrototype proto)
    {
        var hasUsefulStep = false;
        DamageSpecifier? currentDamage = null;
        if (TryComp<DamageableComponent>(patient, out var damageable))
            currentDamage = _damageable.GetPositiveDamage((patient, damageable));

        TryComp<WoundComponent>(patient, out var wounds);

        foreach (var step in proto.Steps)
        {
            var healing = step.GetHealing();
            if (healing != null)
            {
                hasUsefulStep = true;

                if (currentDamage != null &&
                    HasTreatableDamage(currentDamage, healing, FixedPoint2.New(SurgeryConstants.HealingDamageEpsilon)))
                    return true;
            }

            if (step.GetEffect() is ClearWoundCategoryEffect clear)
            {
                hasUsefulStep = true;

                if (wounds != null && _wounds.GetWorstTier(wounds, clear.Category) > 0)
                    return true;
            }
        }

        return !hasUsefulStep;
    }

    /// <summary>
    /// True if a repeatable healing step still has eligible damage left to heal, so the auto-repeat
    /// loop should run another iteration.
    /// </summary>
    private bool StepCanStillHeal(EntityUid patient, SurgeryStep step)
    {
        var healing = step.GetHealing();
        if (healing == null || (step.GetHealingFlat() <= 0 && step.GetHealingMultiplier() <= 0))
            return false;

        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return false;

        var currentDamage = _damageable.GetPositiveDamage((patient, damageable));
        return HasTreatableDamage(currentDamage, healing, FixedPoint2.Zero);
    }

    /// <summary>
    /// True if any of the healing step's eligible damage types is present on the patient above
    /// <paramref name="threshold"/>. Shared by the menu "anything to tend" gate and the repeat loop.
    /// </summary>
    private static bool HasTreatableDamage(DamageSpecifier currentDamage, DamageSpecifier healing, FixedPoint2 threshold)
    {
        foreach (var type in healing.DamageDict.Keys)
        {
            if (currentDamage.DamageDict.TryGetValue(type, out var amount) && amount > threshold)
                return true;
        }

        return false;
    }
}

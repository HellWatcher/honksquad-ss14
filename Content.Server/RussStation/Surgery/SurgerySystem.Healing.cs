using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.RussStation.Surgery.Effects;

namespace Content.Server.RussStation.Surgery;

public sealed partial class SurgerySystem
{
    private void ApplyStepEffects(EntityUid patient, SurgeryStep step)
    {
        if (step.Damage != null)
            _damageable.TryChangeDamage(patient, step.Damage);

        if (step.Healing != null)
        {
            if ((step.HealingFlat > 0 || step.HealingMultiplier > 0) &&
                TryComp<DamageableComponent>(patient, out var damageable))
            {
                // Calculate healing budget: flat + (total_eligible_damage * multiplier)
                var currentDamage = _damageable.GetPositiveDamage((patient, damageable));
                var totalDamage = FixedPoint2.Zero;

                foreach (var type in step.Healing.DamageDict.Keys)
                {
                    if (currentDamage.DamageDict.TryGetValue(type, out var current))
                        totalDamage += current;
                }

                if (totalDamage > 0)
                {
                    var budget = FixedPoint2.New(step.HealingFlat + (float) totalDamage * step.HealingMultiplier);

                    // Distribute proportionally across eligible damage types
                    var healSpec = new DamageSpecifier();
                    foreach (var type in step.Healing.DamageDict.Keys)
                    {
                        if (currentDamage.DamageDict.TryGetValue(type, out var current) && current > 0)
                        {
                            var share = budget * current / totalDamage;
                            healSpec.DamageDict[type] = -share;
                        }
                    }

                    _damageable.TryChangeDamage(patient, healSpec, true);
                }
            }
            else
            {
                // No formula: heal each type independently by listed amount
                var negated = new DamageSpecifier(step.Healing);
                foreach (var key in negated.DamageDict.Keys.ToList())
                {
                    negated.DamageDict[key] = -negated.DamageDict[key];
                }

                _damageable.TryChangeDamage(patient, negated, true);
            }
        }

        if (step.BleedModifier != 0)
            _bloodstream.TryModifyBleedAmount((patient, null), step.BleedModifier);
    }

    private void ApplyCauteryClose(EntityUid patient, EntityUid? surgeon)
    {
        // Cautery burn damage
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Heat", FixedPoint2.New(SurgeryConstants.CauteryBurnDamage));
        _damageable.TryChangeDamage(patient, damage);

        // Stop all bleeding
        _bloodstream.TryModifyBleedAmount((patient, null), SurgeryConstants.CauteryBleedClearAmount);

        if (surgeon != null)
            _popup.PopupEntity(Loc.GetString("surgery-step-cauterize", ("user", surgeon.Value), ("target", patient)), patient);

        // Clean up
        RemComp<ActiveSurgeryComponent>(patient);
        RemComp<SurgeryDrapedComponent>(patient); // Triggers OnDrapedShutdown -> drops bedsheet
    }

    private void HandleEffect(EntityUid? surgeon, EntityUid patient, ISurgeryEffect effect)
    {
        switch (effect)
        {
            case HealDamageEffect heal:
                if (heal.Healing != null)
                {
                    var negated = new DamageSpecifier(heal.Healing);
                    foreach (var key in negated.DamageDict.Keys.ToList())
                    {
                        negated.DamageDict[key] = -negated.DamageDict[key];
                    }

                    _damageable.TryChangeDamage(patient, negated, true);
                }

                break;

            case RemoveOrganEffect:
                OpenOrganRemovalMenu(surgeon, patient);
                break;

            case ClearWoundCategoryEffect clear:
                _wounds.ClearWoundsByCategory(patient, clear.Category);
                break;

            default:
                Log.Warning($"Unhandled surgery effect type: {effect.GetType().Name} on {ToPrettyString(patient)}");
                break;
        }
    }

    /// <summary>
    /// True if any healing step in the procedure has at least one target damage type
    /// the patient currently carries above a small epsilon. Procedures whose only
    /// useful effect is healing (tend-wounds) fail this check on an uninjured patient.
    /// Procedures with no healing steps (e.g. organ manipulation) always pass.
    /// </summary>
    private bool ProcedureHasAnythingToTend(EntityUid patient, SurgeryProcedurePrototype proto)
    {
        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return true;

        var hasHealingStep = false;
        var currentDamage = _damageable.GetPositiveDamage((patient, damageable));

        foreach (var step in proto.Steps)
        {
            if (step.Healing == null)
                continue;

            hasHealingStep = true;

            foreach (var type in step.Healing.DamageDict.Keys)
            {
                if (currentDamage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.New(SurgeryConstants.HealingDamageEpsilon))
                    return true;
            }
        }

        return !hasHealingStep;
    }

    private bool StepCanStillHeal(EntityUid patient, SurgeryStep step)
    {
        if (step.Healing == null || (step.HealingFlat <= 0 && step.HealingMultiplier <= 0))
            return false;

        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return false;

        var currentDamage = _damageable.GetPositiveDamage((patient, damageable));

        foreach (var type in step.Healing.DamageDict.Keys)
        {
            if (currentDamage.DamageDict.TryGetValue(type, out var amount) && amount > 0)
                return true;
        }

        return false;
    }
}

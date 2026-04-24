using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.RussStation.Surgery.Effects;
using Content.Shared.RussStation.Wounds;

namespace Content.Server.RussStation.Surgery;

public sealed partial class SurgerySystem
{
    private void ApplyStepEffects(EntityUid patient, SurgeryStep step)
    {
        var damage = step.GetDamage();
        if (damage != null)
            _damageable.TryChangeDamage(patient, damage);

        var healing = step.GetHealing();
        if (healing != null)
        {
            var healingFlat = step.GetHealingFlat();
            var healingMultiplier = step.GetHealingMultiplier();
            if ((healingFlat > 0 || healingMultiplier > 0) &&
                TryComp<DamageableComponent>(patient, out var damageable))
            {
                // Calculate healing budget: flat + (total_eligible_damage * multiplier)
                var currentDamage = _damageable.GetPositiveDamage((patient, damageable));
                var totalDamage = FixedPoint2.Zero;

                foreach (var type in healing.DamageDict.Keys)
                {
                    if (currentDamage.DamageDict.TryGetValue(type, out var current))
                        totalDamage += current;
                }

                if (totalDamage > 0)
                {
                    var budget = FixedPoint2.New(healingFlat + (float) totalDamage * healingMultiplier);

                    // Distribute proportionally across eligible damage types
                    var healSpec = new DamageSpecifier();
                    foreach (var type in healing.DamageDict.Keys)
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
                var negated = new DamageSpecifier(healing);
                foreach (var key in negated.DamageDict.Keys.ToList())
                {
                    negated.DamageDict[key] = -negated.DamageDict[key];
                }

                _damageable.TryChangeDamage(patient, negated, true);
            }
        }

        var bleed = step.GetBleedPreset() switch
        {
            SurgeryBleedPreset.Incision => SurgeryConstants.IncisionBleedAmount,
            SurgeryBleedPreset.ClampFull => -SurgeryConstants.IncisionBleedAmount,
            _ => step.GetBleedModifier(),
        };

        if (bleed != 0f)
            _bloodstream.TryModifyBleedAmount((patient, null), bleed);
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

                if (currentDamage != null)
                {
                    foreach (var type in healing.DamageDict.Keys)
                    {
                        if (currentDamage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.New(SurgeryConstants.HealingDamageEpsilon))
                            return true;
                    }
                }
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

    private bool StepCanStillHeal(EntityUid patient, SurgeryStep step)
    {
        var healing = step.GetHealing();
        if (healing == null || (step.GetHealingFlat() <= 0 && step.GetHealingMultiplier() <= 0))
            return false;

        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return false;

        var currentDamage = _damageable.GetPositiveDamage((patient, damageable));

        foreach (var type in healing.DamageDict.Keys)
        {
            if (currentDamage.DamageDict.TryGetValue(type, out var amount) && amount > 0)
                return true;
        }

        return false;
    }
}

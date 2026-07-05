using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.RussStation.Damage;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.RussStation.Surgery.Effects;

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
                // Healing budget: flat + (total_eligible_damage * multiplier), distributed
                // proportionally. Shared with implants/potions via HealingBudgetCalculator.
                var currentDamage = _damageable.GetPositiveDamage((patient, damageable));
                var healSpec = HealingBudgetCalculator.Calculate(currentDamage, healing, healingFlat, healingMultiplier);

                if (healSpec != null)
                    _damageable.TryChangeDamage(patient, healSpec, true);
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
        damage.DamageDict.Add(DamageTypeIds.Heat, FixedPoint2.New(SurgeryConstants.CauteryBurnDamage));
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
                Log.Warning("Unhandled surgery effect type: {EffectType} on {Patient}", effect.GetType().Name, ToPrettyString(patient));
                break;
        }
    }
}

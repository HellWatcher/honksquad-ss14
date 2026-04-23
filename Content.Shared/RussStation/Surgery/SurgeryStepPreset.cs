using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.RussStation.Surgery.Effects;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Surgery;

/// <summary>
/// Named templates that fill in a <see cref="SurgeryStep"/>'s tool quality, damage, bleed delta,
/// healing, duration, popup, and repeatable flag from values defined in <see cref="SurgeryConstants"/>
/// rather than duplicating them in every procedure YAML. Fields explicitly set on the step still
/// win over the preset.
/// </summary>
public enum SurgeryStepPreset : byte
{
    /// <summary>Fallback: use the step's own fields.</summary>
    None = 0,

    /// <summary>Opens the patient. Slicing tool, slash damage, full incision bleed.</summary>
    Incision,

    /// <summary>Holds the incision open for the surgeon. Retracting tool, blunt damage.</summary>
    Retract,

    /// <summary>Clamps the incision closed. Clamping tool, reverses the full incision bleed.</summary>
    Clamp,

    /// <summary>Cuts through bone or cavity wall. Sawing tool, slash damage.</summary>
    Saw,

    /// <summary>Tend-wounds treat step for brute damage. Clamping tool with healing budget.</summary>
    TendBrute,

    /// <summary>Tend-wounds treat step for burn damage. Clamping tool with healing budget.</summary>
    TendBurn,

    /// <summary>Remove-organ terminal step. Clamping tool, triggers the organ removal effect.</summary>
    RemoveOrgan,

    /// <summary>Cauterize burn wounds terminal step.</summary>
    CauterizeBurnWounds,

    /// <summary>Manually set a fractured bone.</summary>
    SetBones,
}

/// <summary>
/// Static resolver that maps <see cref="SurgeryStepPreset"/> values onto the fields a
/// <see cref="SurgeryStep"/> would otherwise set by hand. A preset carries a tool quality, a
/// default duration (null falls back to the quality's default), and any damage / healing / bleed
/// preset / popup / effect the step applies.
/// </summary>
public static class SurgeryStepPresets
{
    public readonly record struct PresetDefaults(
        ProtoId<ToolQualityPrototype> Quality,
        float? Duration,
        string? Popup,
        DamageSpecifier? Damage,
        DamageSpecifier? Healing,
        float HealingFlat,
        float HealingMultiplier,
        SurgeryBleedPreset BleedPreset,
        float BleedModifier,
        bool Repeatable,
        ISurgeryEffect? Effect);

    public static PresetDefaults Resolve(SurgeryStepPreset preset)
    {
        switch (preset)
        {
            case SurgeryStepPreset.Incision:
                return new PresetDefaults(
                    Quality: "Slicing",
                    Duration: SurgeryConstants.IncisionDuration,
                    Popup: "surgery-step-incision",
                    Damage: Slash(SurgeryConstants.IncisionSlashDamage),
                    Healing: null,
                    HealingFlat: 0,
                    HealingMultiplier: 0,
                    BleedPreset: SurgeryBleedPreset.Incision,
                    BleedModifier: 0,
                    Repeatable: false,
                    Effect: null);

            case SurgeryStepPreset.Retract:
                return new PresetDefaults(
                    Quality: "Retracting",
                    Duration: SurgeryConstants.RetractDuration,
                    Popup: "surgery-step-retract",
                    Damage: Blunt(SurgeryConstants.RetractBluntDamage),
                    Healing: null,
                    HealingFlat: 0,
                    HealingMultiplier: 0,
                    BleedPreset: SurgeryBleedPreset.Manual,
                    BleedModifier: 0,
                    Repeatable: false,
                    Effect: null);

            case SurgeryStepPreset.Clamp:
                return new PresetDefaults(
                    Quality: "Clamping",
                    Duration: SurgeryConstants.ClampDuration,
                    Popup: "surgery-step-clamp",
                    Damage: null,
                    Healing: null,
                    HealingFlat: 0,
                    HealingMultiplier: 0,
                    BleedPreset: SurgeryBleedPreset.ClampFull,
                    BleedModifier: 0,
                    Repeatable: false,
                    Effect: null);

            case SurgeryStepPreset.Saw:
                return new PresetDefaults(
                    Quality: "Sawing",
                    Duration: SurgeryConstants.SawDuration,
                    Popup: "surgery-step-saw",
                    Damage: Slash(SurgeryConstants.SawSlashDamage),
                    Healing: null,
                    HealingFlat: 0,
                    HealingMultiplier: 0,
                    BleedPreset: SurgeryBleedPreset.Manual,
                    BleedModifier: 0,
                    Repeatable: false,
                    Effect: null);

            case SurgeryStepPreset.TendBrute:
                return new PresetDefaults(
                    Quality: "Clamping",
                    Duration: SurgeryConstants.TendStepDuration,
                    Popup: "surgery-step-treat-brute",
                    Damage: null,
                    Healing: DamageTypes("Blunt", "Slash", "Piercing"),
                    HealingFlat: SurgeryConstants.TendHealingFlat,
                    HealingMultiplier: SurgeryConstants.TendHealingMultiplier,
                    BleedPreset: SurgeryBleedPreset.Manual,
                    BleedModifier: SurgeryConstants.TendBleedReduction,
                    Repeatable: true,
                    Effect: null);

            case SurgeryStepPreset.TendBurn:
                return new PresetDefaults(
                    Quality: "Clamping",
                    Duration: SurgeryConstants.TendStepDuration,
                    Popup: "surgery-step-treat-burn",
                    Damage: null,
                    Healing: DamageTypes("Heat", "Shock", "Cold", "Caustic"),
                    HealingFlat: SurgeryConstants.TendHealingFlat,
                    HealingMultiplier: SurgeryConstants.TendHealingMultiplier,
                    BleedPreset: SurgeryBleedPreset.Manual,
                    BleedModifier: SurgeryConstants.TendBleedReduction,
                    Repeatable: true,
                    Effect: null);

            case SurgeryStepPreset.RemoveOrgan:
                return new PresetDefaults(
                    Quality: "Clamping",
                    Duration: SurgeryConstants.RemoveOrganDuration,
                    Popup: "surgery-step-remove-organ",
                    Damage: null,
                    Healing: null,
                    HealingFlat: 0,
                    HealingMultiplier: 0,
                    BleedPreset: SurgeryBleedPreset.Manual,
                    BleedModifier: 0,
                    Repeatable: true,
                    Effect: new RemoveOrganEffect());

            case SurgeryStepPreset.CauterizeBurnWounds:
                return new PresetDefaults(
                    Quality: "Cauterizing",
                    Duration: SurgeryConstants.WoundRepairDuration,
                    Popup: "surgery-step-treat-burn-wounds",
                    Damage: null,
                    Healing: null,
                    HealingFlat: 0,
                    HealingMultiplier: 0,
                    BleedPreset: SurgeryBleedPreset.Manual,
                    BleedModifier: 0,
                    Repeatable: false,
                    Effect: new ClearWoundCategoryEffect { Category = Content.Shared.RussStation.Wounds.WoundCategory.Burn });

            case SurgeryStepPreset.SetBones:
                return new PresetDefaults(
                    Quality: "BoneSetting",
                    Duration: SurgeryConstants.WoundRepairDuration,
                    Popup: "surgery-step-set-bones",
                    Damage: null,
                    Healing: null,
                    HealingFlat: 0,
                    HealingMultiplier: 0,
                    BleedPreset: SurgeryBleedPreset.Manual,
                    BleedModifier: 0,
                    Repeatable: false,
                    Effect: new ClearWoundCategoryEffect { Category = Content.Shared.RussStation.Wounds.WoundCategory.Fracture });

            default:
                return new PresetDefaults(
                    Quality: default!,
                    Duration: null,
                    Popup: null,
                    Damage: null,
                    Healing: null,
                    HealingFlat: 0,
                    HealingMultiplier: 0,
                    BleedPreset: SurgeryBleedPreset.Manual,
                    BleedModifier: 0,
                    Repeatable: false,
                    Effect: null);
        }
    }

    private static DamageSpecifier Slash(float amount)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict["Slash"] = FixedPoint2.New(amount);
        return spec;
    }

    private static DamageSpecifier Blunt(float amount)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict["Blunt"] = FixedPoint2.New(amount);
        return spec;
    }

    private static DamageSpecifier DamageTypes(params string[] types)
    {
        var spec = new DamageSpecifier();
        foreach (var t in types)
            spec.DamageDict[t] = FixedPoint2.New(1);
        return spec;
    }
}

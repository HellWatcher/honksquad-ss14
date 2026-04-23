using Content.Shared.Damage;
using Content.Shared.RussStation.Surgery.Effects;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Surgery;

/// <summary>
/// Helpers that read a surgery step's effective field values, honouring the preset defaults when
/// the step doesn't override them. Avoids scattering preset-resolution logic across the system
/// code paths that touch steps.
/// </summary>
public static class SurgeryStepExtensions
{
    public static ProtoId<ToolQualityPrototype> GetQuality(this SurgeryStep step)
    {
        if (step.Quality is { } explicitQuality)
            return explicitQuality;
        return SurgeryStepPresets.Resolve(step.Preset).Quality;
    }

    public static float? GetDuration(this SurgeryStep step)
    {
        if (step.Duration is { } explicitDuration)
            return explicitDuration;
        return SurgeryStepPresets.Resolve(step.Preset).Duration;
    }

    public static string GetPopup(this SurgeryStep step)
    {
        if (!string.IsNullOrEmpty(step.Popup))
            return step.Popup;
        return SurgeryStepPresets.Resolve(step.Preset).Popup ?? string.Empty;
    }

    public static DamageSpecifier? GetDamage(this SurgeryStep step)
    {
        return step.Damage ?? SurgeryStepPresets.Resolve(step.Preset).Damage;
    }

    public static DamageSpecifier? GetHealing(this SurgeryStep step)
    {
        return step.Healing ?? SurgeryStepPresets.Resolve(step.Preset).Healing;
    }

    public static float GetHealingFlat(this SurgeryStep step)
    {
        return step.HealingFlat != 0 ? step.HealingFlat : SurgeryStepPresets.Resolve(step.Preset).HealingFlat;
    }

    public static float GetHealingMultiplier(this SurgeryStep step)
    {
        return step.HealingMultiplier != 0 ? step.HealingMultiplier : SurgeryStepPresets.Resolve(step.Preset).HealingMultiplier;
    }

    public static SurgeryBleedPreset GetBleedPreset(this SurgeryStep step)
    {
        if (step.BleedPreset != SurgeryBleedPreset.Manual)
            return step.BleedPreset;
        return SurgeryStepPresets.Resolve(step.Preset).BleedPreset;
    }

    public static float GetBleedModifier(this SurgeryStep step)
    {
        if (step.BleedModifier != 0)
            return step.BleedModifier;
        return SurgeryStepPresets.Resolve(step.Preset).BleedModifier;
    }

    public static bool GetRepeatable(this SurgeryStep step)
    {
        if (step.Repeatable)
            return true;
        return SurgeryStepPresets.Resolve(step.Preset).Repeatable;
    }

    public static ISurgeryEffect? GetEffect(this SurgeryStep step)
    {
        return step.Effect ?? SurgeryStepPresets.Resolve(step.Preset).Effect;
    }
}

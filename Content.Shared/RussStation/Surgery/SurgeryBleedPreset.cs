namespace Content.Shared.RussStation.Surgery;

/// <summary>
/// Named bleed-delta presets that resolve to constants in <see cref="SurgeryConstants"/> at step
/// application time, so procedure YAMLs don't have to duplicate the same float everywhere.
/// </summary>
public enum SurgeryBleedPreset : byte
{
    /// <summary>Use the step's explicit <c>bleedModifier</c> field. Default.</summary>
    Manual = 0,

    /// <summary>Full-depth incision bleed. Adds <see cref="SurgeryConstants.IncisionBleedAmount"/>.</summary>
    Incision,

    /// <summary>Full hemostat clamp. Subtracts <see cref="SurgeryConstants.IncisionBleedAmount"/>.</summary>
    ClampFull,
}

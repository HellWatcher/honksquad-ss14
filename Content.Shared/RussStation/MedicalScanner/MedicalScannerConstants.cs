namespace Content.Shared.RussStation.MedicalScanner;

public static class MedicalScannerConstants
{
    public static readonly TimeSpan DefaultReagentUpdateInterval = TimeSpan.FromSeconds(1);

    public const float DefaultMaxReagentScanRange = 2.5f;

    public const int GroupSeparation = 2;

    public const int ReagentRowIndent = 8;

    public const int ReagentColorSwatchSize = 10;

    public const int ReagentLabelSpacing = 6;

    public const int WoundTierRed = 3;

    public const int WoundTierOrange = 2;

    public const int WoundRowVerticalMargin = 4;

    public const float WoundLabelMaxWidth = 300f;

    /// <summary>
    /// Reagent rows are left-indented only; top/right/bottom padding is zero.
    /// Named so the zero Thickness args aren't naked literals.
    /// </summary>
    public const int ReagentRowOtherSidesPadding = 0;

    /// <summary>
    /// Wound rows have vertical margin only; horizontal margin is zero.
    /// </summary>
    public const int WoundRowHorizontalMargin = 0;

    /// <summary>
    /// Organ list threshold at/above which the "indexed" loc key
    /// ("stomach 1", "stomach 2"…) is used instead of the bare label.
    /// </summary>
    public const int MultiOrganLabelThreshold = 1;

    /// <summary>
    /// 1-based tier offset applied to a zero-based organ index when
    /// generating "stomach N" / "lung N" labels.
    /// </summary>
    public const int OrganIndexLabelOffset = 1;

    /// <summary>
    /// Neutral baseline for movement speed modifiers. Below this, an
    /// effect slows the mob (harmful); above this, it speeds them up (beneficial).
    /// </summary>
    public const float NeutralMovementSpeedModifier = 1f;
}

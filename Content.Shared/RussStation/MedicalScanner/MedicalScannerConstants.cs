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
}

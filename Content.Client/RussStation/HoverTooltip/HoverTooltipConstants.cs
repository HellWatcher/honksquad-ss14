namespace Content.Client.RussStation.HoverTooltip;

public static class HoverTooltipConstants
{
    public const int TooltipFontSize = 12;

    /// <summary>
    /// Text scale factor passed to handle.GetDimensions / handle.DrawString.
    /// Drawing the tooltip at the font's native size, not zoomed.
    /// </summary>
    public const float TooltipTextScale = 1f;

    public const float BackgroundPaddingX = 4f;

    public const float BackgroundPaddingY = 4f;

    /// <summary>
    /// Multiplier applied to per-side padding when growing the background
    /// rectangle: padding appears on both sides of each axis, so the total
    /// extent grows by 2 × padding.
    /// </summary>
    public const float BackgroundPaddingBothSidesMultiplier = 2f;

    public const float CursorOffsetX = 16f;

    public const float CursorOffsetY = 16f;

    /// <summary>
    /// Alpha channel (0..1) for the tooltip's translucent black background.
    /// </summary>
    public const float BackgroundAlpha = 0.65f;
}


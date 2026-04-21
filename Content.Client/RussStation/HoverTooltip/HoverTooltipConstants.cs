namespace Content.Client.RussStation.HoverTooltip;

public static class HoverTooltipConstants
{
    public const int TooltipFontSize = 12;

    /// <summary>
    /// Text scale factor passed to handle.GetDimensions / handle.DrawString.
    /// Drawing the tooltip at the font's native size, not zoomed.
    /// </summary>
    public const float TooltipTextScale = 1f;
}

// HONK — constants for the floating chat input widget. See issue #577.

namespace Content.Client.RussStation.Chat;

internal static class FloatingChatInputConstants
{
    /// <summary>World-space vertical offset above the anchor entity's origin.</summary>
    public const float EntityVerticalOffset = 0.6f;

    /// <summary>Minimum width of the input panel in pixels.</summary>
    public const int InputMinWidth = 320;

    /// <summary>Background RGB for the input panel — dark, cool-neutral.</summary>
    public const byte BackgroundRed = 30;
    public const byte BackgroundGreen = 30;
    public const byte BackgroundBlue = 34;
}

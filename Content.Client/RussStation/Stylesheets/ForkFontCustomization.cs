using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.UserInterface;

namespace Content.Client.RussStation.Stylesheets;

/// <summary>
/// Fork-owned helper for applying runtime font customization to a stylesheet's
/// base font and size list. Replaces the byte-identical HONK block that used to
/// live in both <c>NanotrasenStylesheet</c> and <c>SystemStylesheet</c>.
/// </summary>
public static class ForkFontCustomization
{
    /// <summary>
    /// Configures <paramref name="baseFont"/> from the current font manager settings
    /// and returns a scaled copy of <paramref name="defaultSizes"/> if the user's
    /// primary font size differs from <paramref name="primaryFontSize"/>.
    /// </summary>
    public static List<(string?, int)> Apply(
        NotoFontFamilyStack baseFont,
        StylesheetManager man,
        int primaryFontSize,
        int fontSizeStep,
        List<(string?, int)> defaultSizes)
    {
        baseFont.SetPrimaryFont(man.FontManager.GetFontPathTemplate(), man.FontManager.GetAvailableKinds());

        var customSize = man.FontManager.CurrentSize;
        if (customSize == primaryFontSize)
            return defaultSizes;

        return new List<(string?, int)>
        {
            (null, customSize),
            (StyleClass.FontSmall, customSize - fontSizeStep),
            (StyleClass.FontLarge, customSize + fontSizeStep),
        };
    }
}

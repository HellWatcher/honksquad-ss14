using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.RussStation.UI;

/// <summary>
///     Fork-standard auto-fit helper for Labels that need to stay within a
///     bounded tile without being clipped. Each call site declares the size
///     the label would render at when the game's UI font is at its default
///     (<see cref="GameDefaultUIFontSize"/>, 12pt), and the fitter scales that
///     proportionally to the user's current UI font CVar. Width and optional
///     height budgets shrink it further if the result would overflow the tile.
///
///     Fonts come from the fork <see cref="FontCustomizationManager"/>, so the
///     user's custom font family and the caller's requested kind (Regular,
///     Bold, Italic, BoldItalic) are both preserved.
/// </summary>
/// <remarks>
///     Call whenever the text or the available space changes. A floor
///     (<see cref="MinSize"/>) keeps text readable in narrow tiles.
/// </remarks>
public static class HonkLabelFitter
{
    public const int MinSize = 6;
    public const int AbsoluteMaxSize = 32;
    public const int GameDefaultUIFontSize = 12;

    /// <summary>
    ///     Pick a font size for <paramref name="text"/>, starting from
    ///     <paramref name="baseSizeAtGameDefault"/> scaled by the user's UI
    ///     font preference, then shrunk to fit the width (and optional height)
    ///     budget. Apply it as the Label's FontOverride, using the user's
    ///     current font family and the requested <paramref name="kind"/>.
    /// </summary>
    /// <param name="baseSizeAtGameDefault">
    ///     Size this label would render at when the game's UI font is at its
    ///     default (<see cref="GameDefaultUIFontSize"/>). Effective
    ///     pre-measurement max is
    ///     <c>baseSizeAtGameDefault * userUIFontSize / GameDefaultUIFontSize</c>.
    /// </param>
    /// <param name="kind">
    ///     Font kind to match the label's existing style (pass Bold for
    ///     bold style-class labels, etc.).
    /// </param>
    public static void Fit(
        Label label,
        string text,
        float targetWidthPx,
        int baseSizeAtGameDefault,
        FontKind kind = FontKind.Regular,
        float? targetHeightPx = null)
    {
        var size = PickSize(text, targetWidthPx, targetHeightPx, baseSizeAtGameDefault, kind);
        label.FontOverride = GetFont(size, kind);
    }

    private static int PickSize(string text, float targetWidthPx, float? targetHeightPx, int baseSizeAtGameDefault, FontKind kind)
    {
        // Scale the caller's design-time size by the user's UI font preference.
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        var userSize = Math.Clamp(cfg.GetCVar(CCVars.UIFontSize), MinSize, AbsoluteMaxSize);
        var scaled = (int) MathF.Round(baseSizeAtGameDefault * (float) userSize / GameDefaultUIFontSize);
        var maxSize = Math.Clamp(scaled, MinSize, AbsoluteMaxSize);

        if (string.IsNullOrEmpty(text))
            return maxSize;

        var size = maxSize;
        if (targetWidthPx > 0f)
        {
            var measuredW = MeasureText(GetFont(maxSize, kind), text);
            if (measuredW > targetWidthPx)
            {
                size = (int) MathF.Floor(maxSize * targetWidthPx / measuredW);
                size = Math.Clamp(size, MinSize, maxSize);
            }
        }

        if (targetHeightPx.HasValue && targetHeightPx.Value > 0f)
        {
            var lineHeight = GetFont(size, kind).GetLineHeight(1f);
            if (lineHeight > targetHeightPx.Value)
            {
                var shrunk = (int) MathF.Floor(size * targetHeightPx.Value / lineHeight);
                size = Math.Clamp(shrunk, MinSize, size);
            }
        }

        return size;
    }

    private static Font GetFont(int size, FontKind kind)
    {
        // Route through the fork font manager so the user's custom family and
        // requested kind (bold/italic) both carry through.
        // During early startup (XAML-populated widgets constructed before the
        // stylesheet manager finishes initializing) FontManager can be null,
        // so fall back to the built-in NotoSans resource in that case.
        if (IoCManager.Resolve<IStylesheetManager>() is StylesheetManager man
            && man.FontManager is { } fonts)
        {
            return fonts.GetCurrentFont(size, kind);
        }

        var cache = IoCManager.Resolve<IResourceCache>();
        var kindStr = kind.AsFileName();
        return cache.GetFont($"/Fonts/NotoSans/NotoSans-{kindStr}.ttf", size);
    }

    private static float MeasureText(Font font, string text)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }
        return width;
    }

    /// <summary>
    ///     Fit <paramref name="text"/> into the panel, preferring shrink over wrap so
    ///     the layout stays on one line whenever it still reads. Order of preference:
    ///     <list type="number">
    ///         <item>single line at the user-scaled size,</item>
    ///         <item>single line at a shrunk size, down to a readable floor
    ///             (half the scaled size or <see cref="MinSize"/>, whichever is larger),</item>
    ///         <item>wrap — walk down from the scaled size and take the largest font
    ///             whose wrap stays within <paramref name="maxWrapLines"/>,</item>
    ///         <item>floor at <see cref="MinSize"/> and accept the wrap as-is.</item>
    ///     </list>
    /// </summary>
    public static void FitOrWrap(
        Label label,
        string text,
        float targetWidthPx,
        int baseSizeAtGameDefault,
        FontKind kind = FontKind.Regular,
        int maxWrapLines = 2,
        float? targetHeightPx = null)
    {
        if (string.IsNullOrEmpty(text) || targetWidthPx <= 0f)
        {
            label.Text = text;
            label.FontOverride = GetFont(PickScaledSize(baseSizeAtGameDefault), kind);
            return;
        }

        var scaledSize = PickScaledSize(baseSizeAtGameDefault);
        var readableFloor = Math.Max(MinSize, scaledSize / 2);

        // 1/2: Prefer single-line; shrink down to readableFloor looking for a fit that
        // respects BOTH targetWidthPx and targetHeightPx. Width-only fit isn't enough
        // when the panel caps vertical room (beaker/gun status panels).
        for (var size = scaledSize; size >= readableFloor; size--)
        {
            var font = GetFont(size, kind);
            if (MeasureText(font, text) > targetWidthPx)
                continue;
            if (targetHeightPx.HasValue && font.GetLineHeight(1f) > targetHeightPx.Value)
                continue;
            label.FontOverride = font;
            label.Text = text;
            return;
        }

        // 3: Can't fit single-line at a readable size — allow wrap. Pick the largest font
        // whose wrap stays within both the line budget AND the vertical budget (if given).
        // Line-to-line height is font.GetLineHeight, which includes the line gap Label uses.
        for (var size = scaledSize; size >= MinSize; size--)
        {
            var font = GetFont(size, kind);
            var wrapped = WordWrapAtSize(text, targetWidthPx, font);
            var lines = CountLines(wrapped);
            if (lines > maxWrapLines)
                continue;
            if (targetHeightPx.HasValue && lines * font.GetLineHeight(1f) > targetHeightPx.Value)
                continue;

            label.FontOverride = font;
            label.Text = wrapped;
            return;
        }

        // If wrap is disabled (maxWrapLines=1) and we fell through everything, pick the
        // largest single-line size that fits width at least, so at worst we emit a small font
        // rather than nothing at all.
        if (maxWrapLines == 1)
        {
            for (var size = readableFloor - 1; size >= MinSize; size--)
            {
                var font = GetFont(size, kind);
                if (MeasureText(font, text) > targetWidthPx)
                    continue;
                if (targetHeightPx.HasValue && font.GetLineHeight(1f) > targetHeightPx.Value)
                    continue;
                label.FontOverride = font;
                label.Text = text;
                return;
            }
        }

        // 4: Floor fallback.
        var floorFont = GetFont(MinSize, kind);
        label.FontOverride = floorFont;
        label.Text = WordWrapAtSize(text, targetWidthPx, floorFont);
    }

    private static int PickScaledSize(int baseSizeAtGameDefault)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        var userSize = Math.Clamp(cfg.GetCVar(CCVars.UIFontSize), MinSize, AbsoluteMaxSize);
        return Math.Clamp(
            (int) MathF.Round(baseSizeAtGameDefault * (float) userSize / GameDefaultUIFontSize),
            MinSize,
            AbsoluteMaxSize);
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        var lines = 1;
        foreach (var ch in text)
        {
            if (ch == '\n') lines++;
        }
        return lines;
    }

    private static string WordWrapAtSize(string text, float maxWidthPx, Font font)
    {
        var spaceWidth = MeasureText(font, " ");
        var builder = new System.Text.StringBuilder(text.Length);
        var lineWidth = 0f;
        var firstOnLine = true;
        foreach (var word in text.Split(' '))
        {
            var wordWidth = MeasureText(font, word);
            if (!firstOnLine && lineWidth + spaceWidth + wordWidth > maxWidthPx)
            {
                builder.Append('\n');
                lineWidth = 0f;
                firstOnLine = true;
            }

            if (!firstOnLine)
            {
                builder.Append(' ');
                lineWidth += spaceWidth;
            }

            builder.Append(word);
            lineWidth += wordWidth;
            firstOnLine = false;
        }
        return builder.ToString();
    }

}

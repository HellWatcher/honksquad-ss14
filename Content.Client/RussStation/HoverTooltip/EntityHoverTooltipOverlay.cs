using System.Numerics;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client.RussStation.HoverTooltip;

public sealed class EntityHoverTooltipOverlay : Overlay
{
    private Font _font;
    private readonly FontCustomizationManager? _fontManager;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public string? TooltipText;
    public Vector2 ScreenPosition;
    public bool Visible;

    private static readonly Vector2 CursorOffset = new(HoverTooltipConstants.CursorOffsetX, HoverTooltipConstants.CursorOffsetY);
    private static readonly Color BackgroundColor = Color.Black.WithAlpha(HoverTooltipConstants.BackgroundAlpha);

    public EntityHoverTooltipOverlay(IResourceCache resourceCache)
    {
        // HONK START - Font customization
        var stylesheetMan = IoCManager.Resolve<IStylesheetManager>() as StylesheetManager;
        _fontManager = stylesheetMan?.FontManager;

        if (_fontManager != null)
        {
            _font = _fontManager.GetCurrentFont(HoverTooltipConstants.TooltipFontSize);
            _fontManager.FontsChanged += () => _font = _fontManager.GetCurrentFont(HoverTooltipConstants.TooltipFontSize);
        }
        else
        {
            _font = resourceCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", HoverTooltipConstants.TooltipFontSize);
        }
        // HONK END
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Visible && !string.IsNullOrEmpty(TooltipText);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        handle.SetTransform(Matrix3x2.Identity);

        var pos = ScreenPosition + CursorOffset;
        var dimensions = handle.GetDimensions(_font, TooltipText!, HoverTooltipConstants.TooltipTextScale);

        var bgPos = pos - new Vector2(HoverTooltipConstants.BackgroundPaddingX, HoverTooltipConstants.BackgroundPaddingY);
        var bgSize = dimensions + new Vector2(HoverTooltipConstants.BackgroundPaddingX * HoverTooltipConstants.BackgroundPaddingBothSidesMultiplier, HoverTooltipConstants.BackgroundPaddingY * HoverTooltipConstants.BackgroundPaddingBothSidesMultiplier);
        handle.DrawRect(UIBox2.FromDimensions(bgPos, bgSize), BackgroundColor);

        handle.DrawString(_font, pos, TooltipText!, HoverTooltipConstants.TooltipTextScale, Color.White);
    }
}

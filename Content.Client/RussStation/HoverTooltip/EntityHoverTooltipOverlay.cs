using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client.RussStation.HoverTooltip;

public sealed class EntityHoverTooltipOverlay : Overlay
{
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public string? TooltipText;
    public Vector2 ScreenPosition;
    public bool Visible;

    private const float Padding = 4f;
    private static readonly Vector2 CursorOffset = new(16f, 16f);
    private static readonly Color BackgroundColor = Color.Black.WithAlpha(0.65f);

    public EntityHoverTooltipOverlay(IResourceCache resourceCache)
    {
        _font = resourceCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 12);
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
        var dimensions = handle.GetDimensions(_font, TooltipText!, 1f);

        var bgPos = pos - new Vector2(Padding, Padding);
        var bgSize = dimensions + new Vector2(Padding * 2, Padding * 2);
        handle.DrawRect(UIBox2.FromDimensions(bgPos, bgSize), BackgroundColor);

        handle.DrawString(_font, pos, TooltipText!, 1f, Color.White);
    }
}

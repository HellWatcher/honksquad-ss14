using System.Linq;
using Content.Shared.RussStation.Skillchips.Consumers.Kommand;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client.RussStation.Skillchips.Consumers.Kommand;

/// <summary>
/// Client visualizer for <see cref="KommandEnhancedArrowComponent"/>. Swaps the
/// upstream PointingArrow's stock sprite for the ported SS13 arrow_large_white
/// tinted by the holder's chosen color, with an arrow_large_white_highlights
/// overlay layer that stays white (matching SS13's RESET_COLOR overlay
/// semantic in <c>fancier_pointer</c>). The component arrives via the
/// AutoNetworkedField sync; AfterAutoHandleStateEvent reapplies if the color
/// changes mid-animation.
/// </summary>
public sealed partial class KommandClientSystem : EntitySystem
{
    private const string KommandRsi = "@RussStation/Interface/Misc/kommand_pointer.rsi";
    private const string WhiteState = "arrow_large_white";
    private const string HighlightsState = "arrow_large_white_highlights";

    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<KommandEnhancedArrowComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<KommandEnhancedArrowComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnInit(Entity<KommandEnhancedArrowComponent> ent, ref ComponentInit args)
        => Apply(ent);

    private void OnAfterState(Entity<KommandEnhancedArrowComponent> ent, ref AfterAutoHandleStateEvent args)
        => Apply(ent);

    private void Apply(Entity<KommandEnhancedArrowComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        // Layer 0 is the stock arrow layer from the YAML; swap it to the colored base.
        _sprite.LayerSetData((ent.Owner, sprite), 0, new PrototypeLayerData
        {
            RsiPath = KommandRsi,
            State = WhiteState,
            Color = ent.Comp.Color,
        });

        // Highlights overlay rides on top, unaffected by the user's tint. AddLayer is
        // idempotent on repeat applies because we read back the layer count to detect
        // an existing overlay rather than blindly appending.
        if (sprite.AllLayers.Count() < 2)
        {
            _sprite.AddLayer((ent.Owner, sprite), new PrototypeLayerData
            {
                RsiPath = KommandRsi,
                State = HighlightsState,
                Color = Color.White,
            }, index: null);
        }
        else
        {
            _sprite.LayerSetData((ent.Owner, sprite), 1, new PrototypeLayerData
            {
                RsiPath = KommandRsi,
                State = HighlightsState,
                Color = Color.White,
            });
        }
    }
}

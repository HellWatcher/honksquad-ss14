using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Skillchips.Consumers.Kommand;

/// <summary>
/// Stamped on a freshly-spawned PointingArrow when the pointer is a Kommand
/// chip holder. The client visualizer reads this and applies the chosen tint
/// and a size bump to the arrow's sprite.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class KommandEnhancedArrowComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.Red;

    /// <summary>Sprite scale multiplier applied by the client visualizer.</summary>
    [DataField, AutoNetworkedField]
    public float Scale = KommandConstants.ArrowScale;
}

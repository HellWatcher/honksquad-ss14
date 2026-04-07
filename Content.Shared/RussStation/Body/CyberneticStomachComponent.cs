using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker component for advanced cybernetic stomach that reduces hunger decay rate.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticStomachComponent : Component
{
    /// <summary>
    /// Multiplier applied to <see cref="Content.Shared.Nutrition.Components.HungerComponent.BaseDecayRate"/>.
    /// 0.5 = half the normal hunger drain.
    /// </summary>
    [DataField]
    public float DecayMultiplier = 0.5f;

    /// <summary>
    /// Original BaseDecayRate stored on insert, restored on remove.
    /// Avoids float drift from multiply/divide roundtrips.
    /// </summary>
    [ViewVariables]
    public float? OriginalDecayRate;
}

using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker component for advanced cybernetic stomach that reduces hunger decay rate.
/// Networked so EnsureComp/Dirty during entity init doesn't trip; the multiplier itself is
/// only read on the server.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticStomachComponent : Component
{
    /// <summary>
    /// Multiplier applied to <see cref="Content.Shared.Nutrition.Components.HungerComponent.BaseDecayRate"/>.
    /// 0.5 = half the normal hunger drain.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DecayMultiplier = CyberneticOrganConstants.DefaultStomachDecayMultiplier;

    /// <summary>
    /// Original BaseDecayRate stored on insert, restored on remove.
    /// Avoids float drift from multiply/divide roundtrips.
    /// </summary>
    [ViewVariables]
    public float? OriginalDecayRate;
}

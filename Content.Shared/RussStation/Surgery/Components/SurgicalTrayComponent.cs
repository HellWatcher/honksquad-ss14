using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Surgery.Components;

/// <summary>
/// Adjusts tile friction based on fold state (wheels vs no wheels).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgicalTrayComponent : Component
{
    [DataField, AutoNetworkedField]
    public float FoldedFriction = 0.8f;

    [DataField, AutoNetworkedField]
    public float UnfoldedFriction = 0.4f;
}

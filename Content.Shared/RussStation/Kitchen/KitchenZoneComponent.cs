using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Kitchen;

/// <summary>
/// Marks the center of a kitchen zone. Placed as a fixed anchor entity on maps.
/// Systems can query for nearby entities with this component to detect "being in the kitchen".
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KitchenZoneComponent : Component
{
    /// <summary>
    /// Radius in tiles considered "in the kitchen".
    /// </summary>
    [DataField]
    public float Radius = 10f;
}

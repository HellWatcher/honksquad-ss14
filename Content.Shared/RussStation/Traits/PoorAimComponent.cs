using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Increases weapon spread when firing ranged weapons, making the entity less accurate.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PoorAimComponent : Component
{
    /// <summary>
    /// Multiplier for angular deviation from the target direction.
    /// Higher = worse accuracy. Default 2.0 means twice the normal spread.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpreadMultiplier = 2.0f;
}

using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.RussStation.Wounds;

/// <summary>
/// Tracks active wounds (fractures and burns) and bleed source info for display.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundComponent : Component
{
    /// <summary>
    /// Active fracture and burn wounds.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<WoundEntry> ActiveWounds = new();

    /// <summary>
    /// Which damage type (Slash or Piercing) most recently contributed to bleeding.
    /// Used for display name selection on health analyzer.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public string? BleedSourceDamageType;

    /// <summary>
    /// BleedAmount breakpoints for bleeding wound tiers 1/2/3.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float[] BleedTierThresholds = [1f, 3f, 6f];
}

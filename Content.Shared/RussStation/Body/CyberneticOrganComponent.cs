using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Body;

[NetSerializable, Serializable]
public enum CyberneticTier : byte
{
    Basic,
    Standard,
    Advanced,
}

[NetSerializable, Serializable]
public enum CyberneticEmpEffect : byte
{
    None,
    CardiacArrest,
    BreathingFailure,
    Flicker,
    Deafen,
}

/// <summary>
/// Marker + data component for cybernetic organ replacements.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticOrganComponent : Component
{
    [DataField, AutoNetworkedField]
    public CyberneticTier Tier = CyberneticTier.Basic;

    /// <summary>
    /// What happens to this organ's host when hit by an EMP.
    /// </summary>
    [DataField, AutoNetworkedField]
    public CyberneticEmpEffect EmpEffect = CyberneticEmpEffect.None;

    /// <summary>
    /// Multiplier for EMP duration/severity. Higher = more vulnerable.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EmpVulnerability = CyberneticOrganConstants.DefaultEmpVulnerability;
}

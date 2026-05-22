using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Marks an ID card prototype as one issued by Central Command rather than
/// printed at a station ID console. Read by the GENUINE ID Appraisal Now!
/// skillchip consumer. Applied via HONK markers to the CentCom, Death Squad,
/// and ERT card prototypes upstream.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CentcomIssuedComponent : Component
{
}

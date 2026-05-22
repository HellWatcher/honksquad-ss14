using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Tags a nuclear authentication disk (real or forged) as something the
/// <c>disk_verifier</c> skillchip capability can identify on examine. Added
/// fork-side to both <c>NukeDisk</c> and <c>NukeDiskFake</c> so a chip holder
/// can spot the difference without us touching upstream's examine handlers.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NukeDiskVerifiableComponent : Component
{
    /// <summary>True if this disk is a forgery; false if it is the genuine article.</summary>
    [DataField, AutoNetworkedField]
    public bool IsForgery;
}

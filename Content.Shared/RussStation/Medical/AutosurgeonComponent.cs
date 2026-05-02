using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Medical;

/// <summary>
/// Single-use device that installs a pre-loaded cybernetic organ without surgery.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AutosurgeonComponent : Component
{
    /// <summary>
    /// The organ entity prototype this autosurgeon will spawn and install.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId OrganPrototype;

    /// <summary>
    /// How long the installation takes when used on another person.
    /// </summary>
    [DataField]
    public TimeSpan InstallTime = AutosurgeonConstants.DefaultInstallTime;

    /// <summary>
    /// How long the installation takes when used on yourself.
    /// Shorter than normal since you only need to hold still.
    /// </summary>
    [DataField]
    public TimeSpan SelfInstallTime = AutosurgeonConstants.DefaultSelfInstallTime;
}

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
    /// How long the installation takes.
    /// </summary>
    [DataField]
    public TimeSpan InstallTime = TimeSpan.FromSeconds(5);
}

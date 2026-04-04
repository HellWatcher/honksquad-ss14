using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Messenger;

/// <summary>
/// Messenger PDA cartridge. Each instance gets a unique short address on init.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MessengerCartridgeComponent : Component
{
    /// <summary>
    /// Short unique address for this cartridge, like "A3:F2". Generated on MapInit.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public string Address = "";

    /// <summary>
    /// The cartridge UID the user is currently chatting with, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? ActiveConversation;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool Muted;
}

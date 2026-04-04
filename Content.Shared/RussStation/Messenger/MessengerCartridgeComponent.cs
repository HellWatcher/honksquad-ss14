using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Messenger;

/// <summary>
/// Marker component for the messenger PDA cartridge entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MessengerCartridgeComponent : Component
{
    /// <summary>
    /// The entity the user is currently chatting with, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? ActiveConversation;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool Muted;
}

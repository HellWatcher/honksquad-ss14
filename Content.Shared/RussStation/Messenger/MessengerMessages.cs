using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Messenger;

/// <summary>
/// Client asks to open a specific conversation.
/// </summary>
[Serializable, NetSerializable]
public sealed class MessengerOpenConversationMessage : CartridgeMessageEvent
{
    public NetEntity Target;

    public MessengerOpenConversationMessage(NetEntity target)
    {
        Target = target;
    }
}

/// <summary>
/// Client sends a text message to another player.
/// </summary>
[Serializable, NetSerializable]
public sealed class MessengerSendMessage : CartridgeMessageEvent
{
    public NetEntity Target;
    public string Text;

    public MessengerSendMessage(NetEntity target, string text)
    {
        Target = target;
        Text = text;
    }
}

/// <summary>
/// Client requests the contact list (conversation list view).
/// </summary>
[Serializable, NetSerializable]
public sealed class MessengerRequestContactsMessage : CartridgeMessageEvent;

/// <summary>
/// Client toggles mute on the messenger.
/// </summary>
[Serializable, NetSerializable]
public sealed class MessengerToggleMuteMessage : CartridgeMessageEvent;

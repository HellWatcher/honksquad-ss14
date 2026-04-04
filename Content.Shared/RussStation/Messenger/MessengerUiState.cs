using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Messenger;

/// <summary>
/// UI state for the messenger cartridge. Either a contact list or a conversation view.
/// </summary>
[Serializable, NetSerializable]
public sealed class MessengerUiState : BoundUserInterfaceState
{
    /// <summary>
    /// All available contacts from station records.
    /// </summary>
    public List<MessengerContact> Contacts;

    /// <summary>
    /// If non-null, the client is viewing a conversation with this entity.
    /// </summary>
    public NetEntity? ActiveConversation;

    /// <summary>
    /// Messages in the active conversation, if one is open.
    /// </summary>
    public List<MessengerMessageEntry>? Messages;

    public bool Muted;

    public bool HasId;

    public string Address;

    public MessengerUiState(List<MessengerContact> contacts, NetEntity? activeConversation, List<MessengerMessageEntry>? messages, bool muted, bool hasId, string address)
    {
        Contacts = contacts;
        ActiveConversation = activeConversation;
        Messages = messages;
        Muted = muted;
        HasId = hasId;
        Address = address;
    }
}

/// <summary>
/// A single message in a conversation.
/// </summary>
[Serializable, NetSerializable]
public sealed class MessengerMessageEntry
{
    public string SenderName;
    public string Text;
    public TimeSpan Timestamp;
    public bool FromSelf;

    public MessengerMessageEntry(string senderName, string text, TimeSpan timestamp, bool fromSelf)
    {
        SenderName = senderName;
        Text = text;
        Timestamp = timestamp;
        FromSelf = fromSelf;
    }
}

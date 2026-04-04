using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Messenger;

/// <summary>
/// A contact entry for the messenger contact list, built from station records.
/// </summary>
[Serializable, NetSerializable]
public sealed class MessengerContact
{
    public NetEntity Owner;
    public string Name;
    public string JobTitle;
    public string JobIcon;
    public bool HasUnread;
    public bool ReadOnly;

    public MessengerContact(NetEntity owner, string name, string jobTitle, string jobIcon, bool hasUnread = false, bool readOnly = false)
    {
        Owner = owner;
        Name = name;
        JobTitle = jobTitle;
        JobIcon = jobIcon;
        HasUnread = hasUnread;
        ReadOnly = readOnly;
    }
}

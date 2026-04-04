using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Messenger;

/// <summary>
/// A contact entry representing a messenger cartridge.
/// </summary>
[Serializable, NetSerializable]
public sealed class MessengerContact
{
    /// <summary>
    /// NetEntity of the cartridge, used to address messages.
    /// </summary>
    public NetEntity Cartridge;
    public string Name;
    public string JobTitle;
    public string JobIcon;
    public bool HasUnread;
    public bool ReadOnly;

    public MessengerContact(NetEntity cartridge, string name, string jobTitle, string jobIcon, bool hasUnread = false, bool readOnly = false)
    {
        Cartridge = cartridge;
        Name = name;
        JobTitle = jobTitle;
        JobIcon = jobIcon;
        HasUnread = hasUnread;
        ReadOnly = readOnly;
    }
}

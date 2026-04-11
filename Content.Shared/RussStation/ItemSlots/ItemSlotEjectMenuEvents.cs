using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.ItemSlots;

[Serializable, NetSerializable]
public enum ItemSlotEjectMenuUiKey : byte
{
    Key
}

/// <summary>
///     Client-to-server message requesting ejection of a specific item slot.
/// </summary>
[Serializable, NetSerializable]
public sealed class ItemSlotEjectMenuEjectMessage(string slotId) : BoundUserInterfaceMessage
{
    public string SlotId = slotId;
}

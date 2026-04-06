using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.RussStation.Janitorial;

/// <summary>
///     Enables trash bag pass-through on an entity with item slots.
///     Left-clicking the entity with an item that doesn't fit any slot
///     will attempt to insert it into the trash bag stored in the configured slot.
/// </summary>
[RegisterComponent]
public sealed partial class JanitorialTrolleyComponent : Component
{
    [DataField]
    public string TrashBagSlotId = "trashbag_slot";
}

using Robust.Shared.GameObjects;

namespace Content.Shared.RussStation.ItemSlots;

/// <summary>
///     When added to an entity with <see cref="Content.Shared.Containers.ItemSlot.ItemSlotsComponent"/>,
///     empty-hand interact ejects the first occupied slot and alt-click opens a radial menu
///     to choose which slot to eject.
/// </summary>
[RegisterComponent]
public sealed partial class ItemSlotEjectMenuComponent : Component
{
}

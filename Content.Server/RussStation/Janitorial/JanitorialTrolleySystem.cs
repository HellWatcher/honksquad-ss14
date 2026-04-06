using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.RussStation.Janitorial;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Whitelist;

namespace Content.Server.RussStation.Janitorial;

/// <summary>
///     When a player left-clicks the trolley with an item that doesn't match any
///     item slot whitelist, the system tries to insert it into the trash bag
///     stored in the trolley's trash bag slot.
/// </summary>
public sealed class JanitorialTrolleySystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JanitorialTrolleyComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, JanitorialTrolleyComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots))
            return;

        // If the item matches any trolley slot whitelist, don't intercept.
        // The player should use the verb menu to insert it into the correct slot.
        foreach (var slot in itemSlots.Slots.Values)
        {
            if (_whitelist.IsWhitelistPass(slot.Whitelist, args.Used))
                return;
        }

        // Get the trash bag from its slot.
        if (!_itemSlots.TryGetSlot(uid, comp.TrashBagSlotId, out var trashBagSlot))
            return;

        if (trashBagSlot.Item is not { Valid: true } trashBag)
            return;

        if (!TryComp<StorageComponent>(trashBag, out var storage))
            return;

        // Try to insert the held item into the trash bag's storage.
        if (_storage.PlayerInsertEntityInWorld((trashBag, storage), args.User, args.Used))
            args.Handled = true;
    }
}

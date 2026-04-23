using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;

namespace Content.Shared.RussStation.Janitorial;

/// <summary>
///     Trolley-specific interactions: trash bag pass-through on left-click, and alt-verb filtering
///     so alt-click surfaces Drink / Insert without the per-slot Eject verbs. Ejecting lives on
///     left-click (priority pop) and E (radial menu) instead.
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
        SubscribeLocalEvent<JanitorialTrolleyComponent, GetVerbsEvent<AlternativeVerb>>(
            OnGetAltVerbs,
            after: [typeof(ItemSlotsSystem)]);
    }

    // Remove the per-slot Eject alt-verbs upstream's ItemSlotsSystem adds. The trolley uses E
    // (radial menu) and empty-hand left-click (priority pop) for ejection; alt-click should only
    // offer Drink and Insert.
    private void OnGetAltVerbs(EntityUid uid, JanitorialTrolleyComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        args.Verbs.RemoveWhere(v => v.Category == VerbCategory.Eject);
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

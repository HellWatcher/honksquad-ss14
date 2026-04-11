using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;

namespace Content.Shared.RussStation.Janitorial;

/// <summary>
///     Trolley-specific interactions: trash bag pass-through on left-click, and
///     routes the drink interaction to ActivateInWorld (E) instead of the alt-verb.
/// </summary>
public sealed class JanitorialTrolleySystem : EntitySystem
{
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JanitorialTrolleyComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<JanitorialTrolleyComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<JanitorialTrolleyComponent, GetVerbsEvent<AlternativeVerb>>(
            OnGetAltVerbs,
            after: [typeof(IngestionSystem)]);
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

    private void OnActivate(EntityUid uid, JanitorialTrolleyComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (_ingestion.TryIngest(args.User, uid))
            args.Handled = true;
    }

    // Remove the drink alt-verb so alt-click stays reserved for the eject menu.
    // Drinking is handled by ActivateInWorld (E) above.
    private void OnGetAltVerbs(EntityUid uid, JanitorialTrolleyComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        var drinkText = Loc.GetString("ingestion-verb-drink");
        args.Verbs.RemoveWhere(v => v.Text == drinkText);
    }
}

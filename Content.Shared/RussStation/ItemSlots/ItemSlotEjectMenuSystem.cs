using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.RussStation.ItemSlots;

public sealed class ItemSlotEjectMenuSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSlotEjectMenuComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<ItemSlotEjectMenuComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ItemSlotEjectMenuComponent, ItemSlotEjectMenuEjectMessage>(OnEjectMessage);
    }

    /// <summary>
    ///     Activate-in-world (E): open the radial eject menu if any slot is occupied.
    /// </summary>
    private void OnActivate(EntityUid uid, ItemSlotEjectMenuComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots))
            return;

        var hasOccupied = false;
        foreach (var slot in itemSlots.Slots.Values)
        {
            if (slot.Item != null)
            {
                hasOccupied = true;
                break;
            }
        }

        if (!hasOccupied)
            return;

        _ui.TryToggleUi(uid, ItemSlotEjectMenuUiKey.Key, args.User);
        args.Handled = true;
    }

    /// <summary>
    ///     Empty-hand click: eject the first occupied slot by priority.
    /// </summary>
    private void OnInteractHand(EntityUid uid, ItemSlotEjectMenuComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots))
            return;

        // Iterate slots in priority order (highest first) and eject the first occupied one.
        foreach (var slot in itemSlots.Slots.Values.OrderByDescending(s => s.Priority))
        {
            if (slot.Item == null)
                continue;

            if (_itemSlots.TryEjectToHands(uid, slot, args.User))
            {
                args.Handled = true;
                return;
            }
        }
    }

    /// <summary>
    ///     Handle client request to eject a specific slot.
    /// </summary>
    private void OnEjectMessage(EntityUid uid, ItemSlotEjectMenuComponent comp, ItemSlotEjectMenuEjectMessage args)
    {
        if (args.Actor is not { Valid: true } user)
            return;

        if (!_itemSlots.TryEject(uid, args.SlotId, user, out var item))
            return;

        _hands.PickupOrDrop(user, item.Value);
        _ui.CloseUi(uid, ItemSlotEjectMenuUiKey.Key, user);
    }
}

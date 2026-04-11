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
        SubscribeLocalEvent<ItemSlotEjectMenuComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<ItemSlotEjectMenuComponent, ItemSlotEjectMenuEjectMessage>(OnEjectMessage);
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
    ///     Alt-click: add a verb that opens the radial eject menu.
    /// </summary>
    private void OnGetAltVerbs(EntityUid uid, ItemSlotEjectMenuComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots))
            return;

        // If the user is holding an item that fits any slot, let the upstream insert verbs win.
        if (args.Using != null)
        {
            foreach (var slot in itemSlots.Slots.Values)
            {
                if (slot.InsertOnInteract)
                    continue;

                if (_itemSlots.CanInsert(uid, args.Using.Value, args.User, slot))
                    return;
            }
        }

        // Only show the radial menu verb if there's at least one occupied slot to eject.
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

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("item-slot-eject-menu-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
            Act = () => _ui.TryToggleUi(uid, ItemSlotEjectMenuUiKey.Key, user),
            Priority = 100
        });
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

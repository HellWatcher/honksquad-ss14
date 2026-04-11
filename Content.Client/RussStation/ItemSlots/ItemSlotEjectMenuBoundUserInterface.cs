using Content.Client.UserInterface.Controls;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.RussStation.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.RussStation.ItemSlots;

[UsedImplicitly]
public sealed class ItemSlotEjectMenuBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<ItemSlotsComponent>(Owner, out var itemSlots))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons(BuildButtons(itemSlots));
        _menu.OpenOverMouseScreenPosition();
    }

    private List<RadialMenuOptionBase> BuildButtons(ItemSlotsComponent itemSlots)
    {
        var buttons = new List<RadialMenuOptionBase>();

        foreach (var (id, slot) in itemSlots.Slots)
        {
            if (slot.Item is not { Valid: true } item)
                continue;

            if (!EntMan.TryGetComponent<MetaDataComponent>(item, out var meta))
                continue;

            var slotId = id;
            var option = new RadialMenuActionOption<string>(OnSlotSelected, slotId)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(item),
                ToolTip = meta.EntityName
            };
            buttons.Add(option);
        }

        return buttons;
    }

    private void OnSlotSelected(string slotId)
    {
        SendMessage(new ItemSlotEjectMenuEjectMessage(slotId));
    }
}

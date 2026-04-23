using System.Linq;
using Content.Client.Actions;
using Content.Shared.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Actions.Controls;

[Virtual]
public class ActionButtonContainer : GridContainer
{
    [Dependency] private readonly IEntityManager _entity = default!;

    public event Action<GUIBoundKeyEventArgs, ActionButton>? ActionPressed;
    public event Action<GUIBoundKeyEventArgs, ActionButton>? ActionUnpressed;
    public event Action<ActionButton>? ActionFocusExited;

    public ActionButtonContainer()
    {
        IoCManager.InjectDependencies(this);
    }

    public ActionButton this[int index]
    {
        get => (ActionButton) GetChild(index);
    }

    //HONK START - fork empty-slot preview: pad up to this many slots with nulls so the grid renders
    // the layout visibly even when the player hasn't filled every slot yet.
    public int HonkMinSlotCount { get; set; }
    //HONK END

    public void SetActionData(ActionsSystem system, params EntityUid?[] actionTypes)
    {
        var uniqueCount = Math.Min(system.GetClientActions().Count(), actionTypes.Length + 1);
        var keys = ContentKeyFunctions.GetHotbarBoundKeys();
        //HONK START - honour the caller's sparse layout (drag-to-slot places actions at specific indices)
        // and pad empties up to the fork-requested minimum, capped at the bound hotbar key count.
        // GetHotbarBoundKeys includes HotbarShift1-0, so slots 10-19 default to Shift+Num1..0.
        uniqueCount = Math.Max(uniqueCount, actionTypes.Length);
        if (HonkMinSlotCount > 0)
            uniqueCount = Math.Max(uniqueCount, Math.Min(HonkMinSlotCount, keys.Length));
        //HONK END

        for (var i = 0; i < uniqueCount; i++)
        {
            if (i >= ChildCount)
            {
                AddChild(MakeButton(i));
            }

            if (!actionTypes.TryGetValue(i, out var action))
                action = null;
            ((ActionButton) GetChild(i)).UpdateData(action, system);
        }

        for (var i = ChildCount - 1; i >= uniqueCount; i--)
        {
            RemoveChild(GetChild(i));
        }

        ActionButton MakeButton(int index)
        {
            var button = new ActionButton(_entity);

            //HONK START - per-slot hotkey lookup via SlotHotkeyController: user
            // overrides and slots past the default hotbar range land on the right
            // label. Falls back to the upstream fixed-index key when the UI
            // subsystem isn't wired yet (tests, early init).
            var resolved = UserInterfaceManager
                .GetUIController<Content.Client.RussStation.ActionBar.SlotHotkeyController>()
                .GetHotkeyForSlot(index);
            if (resolved is { } slotKey)
                button.KeyBind = slotKey;
            else if (keys.TryGetValue(index, out var boundKey))
                button.KeyBind = boundKey;
            //HONK END

            return button;
        }
    }

    public void ClearActionData()
    {
        foreach (var button in Children)
        {
            ((ActionButton) button).ClearData();
        }
    }

    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);

        if (newChild is not ActionButton button)
            return;

        button.ActionPressed += ActionPressed;
        button.ActionUnpressed += ActionUnpressed;
        button.ActionFocusExited += ActionFocusExited;
    }

    protected override void ChildRemoved(Control newChild)
    {
        if (newChild is not ActionButton button)
            return;

        button.ActionPressed -= ActionPressed;
        button.ActionUnpressed -= ActionUnpressed;
        button.ActionFocusExited -= ActionFocusExited;
    }

    public bool TryGetButtonIndex(ActionButton button, out int position)
    {
        if (button.Parent != this)
        {
            position = 0;
            return false;
        }

        position = button.GetPositionInParent();
        return true;
    }

    public IEnumerable<ActionButton> GetButtons()
    {
        foreach (var control in Children)
        {
            if (control is ActionButton button)
                yield return button;
        }
    }
}

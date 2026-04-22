using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client.RussStation.UI;

// HONK Right-click anywhere inside a focused text box clears it. Mirrors the
// "clear" affordance the upstream actions menu used to ship as a button, but
// applies to every LineEdit / HistoryLineEdit in the client. We subscribe to
// the input manager's UI-scope keybind event so we can inspect the function
// (UIManager.OnKeyBindDown only surfaces the Control, not the function).
public sealed class RightClickClearTextBoxController : UIController
{
    [Dependency] private readonly IInputManager _input = default!;

    public override void Initialize()
    {
        base.Initialize();
        _input.UIKeyBindStateChanged += OnUIKeyBind;
    }

    private bool OnUIKeyBind(BoundKeyEventArgs args)
    {
        if (args.State != BoundKeyState.Down || args.Function != EngineKeyFunctions.UIRightClick)
            return false;

        var hovered = UIManager.MouseGetControl(args.PointerLocation);
        if (hovered is not LineEdit line || UIManager.KeyboardFocused != line || !line.Editable)
            return false;

        // Clear() sets Text directly and suppresses OnTextChanged, so callers that
        // react to search input wouldn't see the clear. Use SetText with invokeEvent.
        line.SetText(string.Empty, invokeEvent: true);
        args.Handle();
        return true;
    }
}

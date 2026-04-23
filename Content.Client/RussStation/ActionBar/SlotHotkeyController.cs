using System.Linq;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input;

namespace Content.Client.RussStation.ActionBar;

// HONK Per-slot hotkey assignment for the fork's resizable action bar (#579).
// Each slot uses the BoundKeyFunction at the same index in ContentKeyFunctions.
// GetHotbarBoundKeys (Hotbar1-0 for slots 0-9, HotbarShift1-0 for slots 10-19
// by default). Assign mode rebinds the physical key for that function via
// IInputManager, so Settings → Controls is the single source of truth and the
// action bar labels refresh when the user changes the binding from either side.
[UsedImplicitly]
public sealed class SlotHotkeyController : UIController
{
    [Dependency] private readonly IInputManager _input = default!;

    private bool _assignMode;
    private int? _armedSlot;

    public bool AssignMode => _assignMode;

    /// <summary>Raised whenever the assign-mode flag or armed slot changes, so
    /// views can refresh any visual indicator they own.</summary>
    public event Action? AssignStateChanged;

    /// <summary>Raised whenever a keybinding relevant to the action bar changes,
    /// so the bar can refresh its labels without each button polling.</summary>
    public event Action? SlotBindingChanged;

    public override void Initialize()
    {
        base.Initialize();
        // FirstChanceOnKeyEvent sees raw key presses before normal dispatch, matching the
        // engine's key-rebind menu. While a slot is armed we consume the event and use it
        // to rebind the slot's function directly through IInputManager.
        _input.FirstChanceOnKeyEvent += OnFirstChanceKey;
        _input.OnKeyBindingAdded += OnBindingChanged;
        _input.OnKeyBindingRemoved += OnBindingChanged;
    }

    /// <summary>Stable slot → function mapping. Fixed for the lifetime of the bar.</summary>
    public static BoundKeyFunction? FunctionForSlot(int slot)
    {
        if (slot < 0)
            return null;
        var hotbar = ContentKeyFunctions.GetHotbarBoundKeys();
        return slot < hotbar.Length ? (BoundKeyFunction?) hotbar[slot] : null;
    }

    private void OnBindingChanged(IKeyBinding _) => SlotBindingChanged?.Invoke();

    private void OnFirstChanceKey(KeyEventArgs keyEvent, KeyEventType type)
    {
        if (!_assignMode || _armedSlot is not { } slot)
            return;

        // Consume on down so gameplay (firing actions, typing into chat, etc.) never sees it.
        // Commit on up so release doesn't immediately trigger the just-bound function.
        keyEvent.Handle();
        if (type != KeyEventType.Up)
            return;

        var key = keyEvent.Key;
        if (IsIgnoredKey(key))
            return;

        if (FunctionForSlot(slot) is not { } function)
            return;

        RebindFunction(function, keyEvent);
        _armedSlot = null;
        AssignStateChanged?.Invoke();
    }

    private static bool IsIgnoredKey(Keyboard.Key key)
    {
        return key == Keyboard.Key.Control || key == Keyboard.Key.Shift || key == Keyboard.Key.Alt
               || key == Keyboard.Key.LSystem || key == Keyboard.Key.RSystem
               || key == Keyboard.Key.MouseLeft || key == Keyboard.Key.MouseRight
               || key == Keyboard.Key.MouseMiddle
               || key == Keyboard.Key.Unknown;
    }

    // Replace every existing binding for this function with a single new binding at the
    // captured key+modifiers, matching what the engine's key-rebind menu does. Saving to
    // user data persists the change so Settings → Controls reflects it immediately.
    private void RebindFunction(BoundKeyFunction function, KeyEventArgs keyEvent)
    {
        foreach (var existing in _input.GetKeyBindings(function).ToArray())
            _input.RemoveBinding(existing);

        var mods = new Keyboard.Key[3];
        var i = 0;
        if (keyEvent.Control && keyEvent.Key != Keyboard.Key.Control)
            mods[i++] = Keyboard.Key.Control;
        if (keyEvent.Shift && keyEvent.Key != Keyboard.Key.Shift)
            mods[i++] = Keyboard.Key.Shift;
        if (keyEvent.Alt && keyEvent.Key != Keyboard.Key.Alt && i < 3)
            mods[i++] = Keyboard.Key.Alt;

        _input.RegisterBinding(new KeyBindingRegistration
        {
            Function = function,
            BaseKey = keyEvent.Key,
            Mod1 = mods[0],
            Mod2 = mods[1],
            Mod3 = mods[2],
            Type = KeyBindingType.State,
            CanFocus = false,
            CanRepeat = false,
            AllowSubCombs = true,
        });
        _input.SaveToUserData();
    }

    /// <summary>Returns the key function that fires the given slot, or null if the
    /// slot index is outside the supported range.</summary>
    public BoundKeyFunction? GetHotkeyForSlot(int slot) => FunctionForSlot(slot);

    public void SetAssignMode(bool value)
    {
        if (_assignMode == value)
            return;
        _assignMode = value;
        _armedSlot = null;
        AssignStateChanged?.Invoke();
    }

    public void ArmSlot(int slot)
    {
        if (!_assignMode)
            return;
        _armedSlot = slot;
        AssignStateChanged?.Invoke();
    }

    public int? ArmedSlot => _armedSlot;
}

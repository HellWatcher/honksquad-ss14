using System.Linq;
using System.Text;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.CCVar;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;

namespace Content.Client.RussStation.ActionBar;

// HONK Per-slot hotkey assignment for the fork's resizable action bar (#579).
// Upstream fires slot N by pressing Hotbar(N+1), which hardwires the bar to
// 10 slots and makes additional rows unreachable from the keyboard. This
// controller stores a slot→key map in a CVar, pre-empts the upstream
// Hotbar0-9 handlers, and exposes an "assign mode" where left-clicking a
// slot arms it so the next hotbar keypress becomes that slot's hotkey.
[UsedImplicitly]
public sealed class SlotHotkeyController : UIController,
    IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IInputManager _input = default!;

    // Explicit slot→key overrides parsed from the CVar. Slots not present
    // here fall back to the upstream default (hotbarKeys[slot] for slot<10).
    private readonly Dictionary<int, BoundKeyFunction> _slotToKey = new();

    // Inverse of the fully-resolved slot→key map, used on keypress to find
    // which slot to trigger. Rebuilt whenever _slotToKey changes.
    private readonly Dictionary<BoundKeyFunction, int> _keyToSlot = new();

    private bool _assignMode;
    private int? _armedSlot;

    public bool AssignMode => _assignMode;

    /// <summary>Raised whenever the assign-mode flag or armed slot changes, so
    /// views can refresh any visual indicator they own.</summary>
    public event Action? AssignStateChanged;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(CCVars.HonkActionBarSlotHotkeys, OnCVarChanged, true);
        // Capture key presses ourselves while assign mode is on. Command-bind BindBefore
        // ordering depends on UIController init order we don't control, so the direct
        // input-manager hook is more reliable and also captures keys outside Hotbar0-9.
        _input.UIKeyBindStateChanged += OnUIKeyBind;
    }

    private bool OnUIKeyBind(BoundKeyEventArgs args)
    {
        if (!_assignMode || _armedSlot is not { } slot)
            return false;
        if (args.State != BoundKeyState.Down)
            return false;
        // Ignore mouse clicks and interaction functions so the click that armed this slot
        // doesn't also get captured as its keybind.
        if (args.Function == EngineKeyFunctions.UIClick
            || args.Function == EngineKeyFunctions.UIRightClick
            || args.Function == EngineKeyFunctions.Use
            || args.Function == EngineKeyFunctions.UseSecondary)
            return false;

        AssignSlotKey(slot, args.Function);
        _armedSlot = null;
        AssignStateChanged?.Invoke();
        args.Handle();
        return true;
    }

    /// <summary>Returns the key that fires the given slot, or null if the
    /// slot has no assigned hotkey (slot past the default hotbar range and
    /// no explicit entry in the CVar).</summary>
    public BoundKeyFunction? GetHotkeyForSlot(int slot)
    {
        if (_slotToKey.TryGetValue(slot, out var explicitKey))
            return explicitKey;

        var hotbar = ContentKeyFunctions.GetHotbarBoundKeys();
        if (slot >= 0 && slot < hotbar.Length)
        {
            // Another slot may have explicitly claimed this key. Fall back to
            // the default only if no explicit claim exists.
            var defaultKey = hotbar[slot];
            if (_keyToSlot.TryGetValue(defaultKey, out var owner) && owner != slot)
                return null;
            return defaultKey;
        }

        return null;
    }

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

    public void OnStateEntered(GameplayState state)
    {
        var hotbar = ContentKeyFunctions.GetHotbarBoundKeys();
        var builder = CommandBinds.Builder;
        foreach (var key in hotbar)
        {
            var captured = key;
            // BindBefore(ActionUIController) ensures we run ahead of upstream's
            // fixed-index Hotbar handler; returning true consumes the event so
            // upstream's version never fires, letting our map fully own dispatch.
            builder = builder.BindBefore(
                captured,
                new PointerInputCmdHandler((in PointerInputCmdArgs args) =>
                {
                    if (args.State != BoundKeyState.Down)
                        return false;
                    return HandleKey(captured);
                }, false, true),
                typeof(ActionUIController));
        }
        builder.Register<SlotHotkeyController>();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<SlotHotkeyController>();
        _armedSlot = null;
    }

    private bool HandleKey(BoundKeyFunction key)
    {
        if (_assignMode && _armedSlot is { } slot)
        {
            AssignSlotKey(slot, key);
            _armedSlot = null;
            AssignStateChanged?.Invoke();
            return true;
        }

        if (_keyToSlot.TryGetValue(key, out var mapped))
        {
            UIManager.GetUIController<ActionUIController>().HonkTriggerSlot(mapped);
            return true;
        }

        // Key has no owning slot (e.g. user remapped the only slot that claimed
        // it). Leave the event unhandled so any other listener can still react.
        return false;
    }

    private void AssignSlotKey(int slot, BoundKeyFunction key)
    {
        // Each key can only fire one slot — strip any previous explicit owner.
        foreach (var other in _slotToKey
                     .Where(kv => kv.Key != slot && kv.Value == key)
                     .Select(kv => kv.Key)
                     .ToList())
        {
            _slotToKey.Remove(other);
        }

        _slotToKey[slot] = key;
        WriteCVar();
        RebuildInverse();
        UIManager.GetUIController<ActionUIController>().HonkRefreshHotbar();
    }

    public void ClearSlotKey(int slot)
    {
        if (!_slotToKey.Remove(slot))
            return;
        WriteCVar();
        RebuildInverse();
        UIManager.GetUIController<ActionUIController>().HonkRefreshHotbar();
    }

    private void WriteCVar()
    {
        var sb = new StringBuilder();
        var first = true;
        foreach (var (slot, key) in _slotToKey.OrderBy(kv => kv.Key))
        {
            if (!first)
                sb.Append(';');
            sb.Append(slot).Append('=').Append(key.FunctionName);
            first = false;
        }
        _cfg.SetCVar(CCVars.HonkActionBarSlotHotkeys, sb.ToString());
    }

    private void OnCVarChanged(string raw)
    {
        _slotToKey.Clear();

        if (!string.IsNullOrWhiteSpace(raw))
        {
            foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = entry.IndexOf('=');
                if (eq <= 0 || eq == entry.Length - 1)
                    continue;
                if (!int.TryParse(entry.AsSpan(0, eq), out var slot) || slot < 0)
                    continue;
                var name = entry[(eq + 1)..];
                _slotToKey[slot] = new BoundKeyFunction(name);
            }
        }

        RebuildInverse();
        UIManager.GetUIController<ActionUIController>().HonkRefreshHotbar();
    }

    private void RebuildInverse()
    {
        _keyToSlot.Clear();
        foreach (var (slot, key) in _slotToKey)
            _keyToSlot[key] = slot;

        var hotbar = ContentKeyFunctions.GetHotbarBoundKeys();
        for (var slot = 0; slot < hotbar.Length; slot++)
        {
            if (_slotToKey.ContainsKey(slot))
                continue;
            var defaultKey = hotbar[slot];
            if (!_keyToSlot.ContainsKey(defaultKey))
                _keyToSlot[defaultKey] = slot;
        }
    }
}

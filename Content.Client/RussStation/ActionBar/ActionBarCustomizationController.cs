using System.Linq;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Actions;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Actions.Windows;
using Content.Shared.CCVar;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;

namespace Content.Client.RussStation.ActionBar;

// HONK Fork UIController that applies user-tunable layout settings to the
// upstream action bar widget. Reads CVars registered in CCVars.ActionBar.cs
// and re-applies them whenever any of them change, so settings take effect
// the moment the user clicks Apply in the options menu. Also re-applies on
// gameplay state entry since the ActionsBar widget only exists then.
public sealed class ActionBarCustomizationController : UIController, IOnStateEntered<GameplayState>
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private int _rows;
    private int _slotsPerRow;
    private int _slotSpacing;
    private bool _showKeybindLabel;

    // Read by ActionButton.UpdateBackground (HONK block) on every frame so empty
    // slots can render a faint outline. A static keeps the hot path free of a
    // UIController lookup per button per frame.
    public static bool ShowEmptySlots { get; private set; }

    // Read by ActionUIController.OnActionAdded (HONK guard) so newly granted actions
    // can skip auto-population when the user wants a curated bar layout.
    public static bool AutoAddActions { get; private set; } = true;

    // Read by ActionUIController drag / right-click paths (HONK guards) to keep
    // the bar immutable when the player has locked the layout.
    public static bool LockActions { get; private set; }

    // Read by the gameplay-screen resize handlers (HONK guards) to keep them from
    // calling MaxGridHeight/MaxGridWidth, which would flip the grid into size-limit
    // mode and silently overwrite the user's explicit row count on resize.
    public static readonly bool OverridesRowLayout = true;

    // Base 0.0-1.0 alpha applied to every action button's slot background. Read each
    // frame by ActionButton.UpdateBackground (HONK block). The empty-slot fade scales
    // proportionally so the relative contrast stays consistent.
    public static float ButtonBackgroundAlpha { get; private set; } = 150f / 255f;

    // Flipped by ActionUIController drag hooks so empty drop targets are padded into the
    // container even when the persistent show-empty toggle is off.
    public static bool IsDragActive { get; private set; }

    // Mirrored from SlotHotkeyController so the bar-side code (UpdateBackground, ApplyLabels)
    // can reveal every slot and its keybind label while the player is assigning hotkeys.
    public static bool AssignHotkeyMode { get; private set; }

    // Emote proto id -> slot index, persisted via honk.action_bar.emote_slots so a player's
    // curated emote layout survives disconnects and server restarts. Read by OnActionAdded
    // through TryGetSavedEmoteSlot so the controller is guaranteed to be instantiated.
    private readonly Dictionary<string, int> _emoteSlots = new();

    public bool TryGetSavedEmoteSlot(string? emoteProtoId, out int slot)
    {
        if (!string.IsNullOrEmpty(emoteProtoId) && _emoteSlots.TryGetValue(emoteProtoId, out slot))
            return true;
        slot = default;
        return false;
    }

    public override void Initialize()
    {
        base.Initialize();

        _rows = _cfg.GetCVar(CCVars.HonkActionBarRows);
        _slotsPerRow = _cfg.GetCVar(CCVars.HonkActionBarSlotsPerRow);
        _slotSpacing = _cfg.GetCVar(CCVars.HonkActionBarSlotSpacing);
        _showKeybindLabel = _cfg.GetCVar(CCVars.HonkActionBarShowKeybindLabel);
        ShowEmptySlots = _cfg.GetCVar(CCVars.HonkActionBarShowEmptySlots);
        AutoAddActions = _cfg.GetCVar(CCVars.HonkActionBarAutoAddActions);
        LockActions = _cfg.GetCVar(CCVars.HonkActionBarLock);
        ButtonBackgroundAlpha = Math.Clamp(_cfg.GetCVar(CCVars.HonkActionBarButtonBackgroundAlpha), 0f, 1f);

        _cfg.OnValueChanged(CCVars.HonkActionBarRows, OnRowsChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarSlotsPerRow, OnSlotsPerRowChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarSlotSpacing, OnSlotSpacingChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarShowKeybindLabel, OnShowKeybindLabelChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarShowEmptySlots, OnShowEmptySlotsChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarAutoAddActions, v => AutoAddActions = v, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarLock, v => LockActions = v, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarButtonBackgroundAlpha,
            v => ButtonBackgroundAlpha = Math.Clamp(v, 0f, 1f), true);
        _cfg.OnValueChanged(CCVars.HonkActionBarEmoteSlots, OnEmoteSlotsChanged, true);

        // Mirror the assign-hotkey toggle so the bar auto-reveals while the player rebinds slots.
        var slotHotkeys = UIManager.GetUIController<SlotHotkeyController>();
        AssignHotkeyMode = slotHotkeys.AssignMode;
        slotHotkeys.AssignStateChanged += OnAssignStateChanged;
        // Rebuild the hotbar when any action-bar slot's binding changes so the labels track
        // what Settings → Controls currently holds.
        slotHotkeys.SlotBindingChanged += RefreshHotbar;
    }

    private void OnAssignStateChanged()
    {
        var slotHotkeys = UIManager.GetUIController<SlotHotkeyController>();
        AssignHotkeyMode = slotHotkeys.AssignMode;
        ApplyLayout();
        ApplyLabels();
        ApplyArmedHighlight();
        RefreshHotbar();
    }

    // Highlight the currently-armed slot so the player has feedback between clicking a slot
    // and pressing the hotbar key that will be assigned to it. Clears all highlights when
    // assign mode is off or no slot is armed.
    private void ApplyArmedHighlight()
    {
        if (GetContainer() is not { } container)
            return;

        var armed = UIManager.GetUIController<SlotHotkeyController>().ArmedSlot;
        var i = 0;
        foreach (var button in container.GetButtons())
        {
            button.HighlightRect.Visible = AssignHotkeyMode && armed == i;
            i++;
        }
    }

    private void OnRowsChanged(int value)
    {
        _rows = Math.Clamp(value, 1, 4);
        ApplyLayout();
        RefreshHotbar();
    }

    private void OnSlotsPerRowChanged(int value)
    {
        _slotsPerRow = Math.Clamp(value, 1, 10);
        ApplyLayout();
        RefreshHotbar();
    }

    private void OnSlotSpacingChanged(int value)
    {
        _slotSpacing = Math.Clamp(value, 0, 16);
        ApplyLayout();
    }

    private void OnShowKeybindLabelChanged(bool value)
    {
        _showKeybindLabel = value;
        ApplyLabels();
    }

    private void OnShowEmptySlotsChanged(bool value)
    {
        ShowEmptySlots = value;
        ApplyLayout();
        RefreshHotbar();
    }

    private ActionButtonContainer? GetContainer()
    {
        var bar = UIManager.GetActiveUIWidgetOrNull<ActionsBar>();
        return bar?.ActionsContainer;
    }

    private void ApplyLayout()
    {
        if (GetContainer() is not { } container)
            return;

        // Setting Columns (not Rows) makes the grid fill row-by-row, so hotkeys 1..0 land
        // in row 1 and 2 lands on row 2's leftmost slot rather than column-major flow.
        container.Columns = _slotsPerRow;
        container.HSeparationOverride = _slotSpacing;
        container.VSeparationOverride = _slotSpacing;
        // Reveal every slot (pad up to rows x slots_per_row) when either the user's persistent
        // toggle is on, a drag is active, or the player is actively rebinding slot hotkeys.
        container.HonkMinSlotCount = ShowEmptySlots || IsDragActive || AssignHotkeyMode
            ? _rows * _slotsPerRow
            : 0;
    }

    private void ApplyLabels()
    {
        if (GetContainer() is not { } container)
            return;

        // Normally labels only render on slots with an action and on drag targets. Assign-hotkey
        // mode forces every slot's label visible so the player can see which key they're rebinding.
        foreach (var button in container.GetButtons())
        {
            button.Label.Visible = AssignHotkeyMode
                || (_showKeybindLabel && (button.Action != null || IsDragActive));
        }
    }

    private void RefreshHotbar()
    {
        if (GetContainer() == null)
            return;
        UIManager.GetUIController<ActionUIController>().HonkRefreshHotbar();
        // Padding may have added buttons; labels must be re-applied to the new ones.
        ApplyLabels();
    }

    private void OnEmoteSlotsChanged(string raw)
    {
        _emoteSlots.Clear();
        if (string.IsNullOrWhiteSpace(raw))
            return;
        var protoMan = IoCManager.Resolve<Robust.Shared.Prototypes.IPrototypeManager>();
        var dropped = false;
        foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = entry.IndexOf('=');
            if (eq <= 0 || eq == entry.Length - 1)
                continue;
            var id = entry[..eq];
            if (!int.TryParse(entry.AsSpan(eq + 1), out var slot) || slot < 0)
                continue;

            // Drop entries whose proto id doesn't match a real EmotePrototype (hand-edited CVar,
            // stale entry from a removed emote, etc.). The server still gates allowlist + per-mob
            // AllowedToUseEmote on grant, so an invalid entry here just means dead weight.
            if (!protoMan.HasIndex<Content.Shared.Chat.Prototypes.EmotePrototype>(id))
            {
                dropped = true;
                continue;
            }

            _emoteSlots[id] = slot;
        }

        // Rewrite the CVar if we filtered anything so the pruned list is what lands on disk.
        if (dropped)
        {
            _cfg.SetCVar(CCVars.HonkActionBarEmoteSlots, SerializeEmoteSlots());
            _cfg.SaveToFile();
        }
    }

    private string SerializeEmoteSlots()
    {
        var sb = new System.Text.StringBuilder();
        var first = true;
        foreach (var (id, slot) in _emoteSlots.OrderBy(kv => kv.Value))
        {
            if (!first)
                sb.Append(';');
            sb.Append(id).Append('=').Append(slot);
            first = false;
        }
        return sb.ToString();
    }

    /// <summary>Persist a saved slot for an emote prototype. Pass null to forget the slot.</summary>
    public void HonkRememberEmoteSlot(string? emoteProtoId, int? slot)
    {
        if (string.IsNullOrEmpty(emoteProtoId))
            return;

        // Only persist real emote prototypes. Stops a bad caller (or a stale entity with
        // garbage in the component) from poisoning the saved layout with unknown ids.
        // The server gates actual dispatch through AllowedToUseEmote, so even if something
        // slipped through here it wouldn't fire for a disallowed species.
        if (slot != null
            && !IoCManager.Resolve<Robust.Shared.Prototypes.IPrototypeManager>()
                .HasIndex<Content.Shared.Chat.Prototypes.EmotePrototype>(emoteProtoId))
        {
            return;
        }

        var changed = false;
        if (slot is { } index)
        {
            // If some other emote used to live in this slot, bump it out so two entries
            // don't race each other back onto the bar on reconnect.
            foreach (var existing in _emoteSlots.Where(kv => kv.Value == index && kv.Key != emoteProtoId)
                         .Select(kv => kv.Key).ToList())
            {
                _emoteSlots.Remove(existing);
                changed = true;
            }

            if (!_emoteSlots.TryGetValue(emoteProtoId, out var prior) || prior != index)
            {
                _emoteSlots[emoteProtoId] = index;
                changed = true;
            }
        }
        else if (_emoteSlots.Remove(emoteProtoId))
        {
            changed = true;
        }

        if (changed)
        {
            _cfg.SetCVar(CCVars.HonkActionBarEmoteSlots, SerializeEmoteSlots());
            _cfg.SaveToFile();
        }
    }

    // Wires the lock + auto-add toggle checkboxes on the actions window, called from
    // the upstream LoadGui (HONK) each time the window is (re)created.
    public void HonkBindWindow(ActionsWindow window)
    {
        window.LockButton.Pressed = LockActions;
        window.AutoAddButton.Pressed = AutoAddActions;
        window.LockButton.OnToggled += a => _cfg.SetCVar(CCVars.HonkActionBarLock, a.Pressed);
        window.AutoAddButton.OnToggled += a => _cfg.SetCVar(CCVars.HonkActionBarAutoAddActions, a.Pressed);
    }

    // Called from the upstream ActionUIController (HONK) once the action bar widget
    // is registered. That's the first point the container reliably exists, so fresh
    // client starts get the stored layout applied here rather than relying on the
    // order in which UIControllers receive OnStateEntered.
    public void HonkOnContainerReady()
    {
        ApplyLayout();
        ApplyLabels();
        RefreshHotbar();
    }

    // Called from the action drag hooks so the container can pad in empty drop targets
    // and then trim them back out once the drop completes.
    public void HonkSetDragActive(bool active)
    {
        if (IsDragActive == active)
            return;
        IsDragActive = active;
        ApplyLayout();
        RefreshHotbar();
        ApplyLabels();
    }

    public void OnStateEntered(GameplayState state)
    {
        // HonkOnContainerReady runs from ActionUIController.LoadGui once the widget is up;
        // nothing else to do here now that MaxGrid* resizes no longer clobber our Rows.
    }
}

using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.UserInterface.Systems.Actions;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Actions.Windows;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;

namespace Content.Client.RussStation.ActionBar;

// HONK Fork UIController that applies user-tunable layout settings to the upstream action
// bar widget. It stays thin: the CVar read/write/subscribe lives in ActionBarCVarManager,
// the preset capture/apply/reset in ActionBarPresetManager, and the float/anchor positioning
// in ActionBarPositioningManager. The controller keeps the layout-application glue (the bits
// that touch the live ActionsBar container) and the wiring between those collaborators.
public sealed class ActionBarCustomizationController : UIController, IOnStateEntered<GameplayState>
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IClientPreferencesManager _prefs = default!;

    private ActionBarCVarManager _cvars = default!;
    private ActionBarPositioningManager _positioning = default!;
    private ActionBarPresetManager _presets = default!;

    private ActionBarPresetsWindow? _presetsWindow;
    private bool _autoLoadedInitialPreset;

    public bool TryGetSavedEmoteSlot(string? emoteId, out int slot)
        => _presets.TryGetSavedEmoteSlot(emoteId, out slot);

    public string GetActiveCharacterName() => _presets.GetActiveCharacterName();

    public ActionBarPreset? FindActivePresetForCharacter() => _presets.FindActivePresetForCharacter();

    public override void Initialize()
    {
        base.Initialize();

        _cvars = new ActionBarCVarManager(_cfg);

        // Layout/label reactions are wired before reading the CVars so the invokeImmediately
        // seeding reaches them (they no-op until the bar widget exists).
        _cvars.LayoutAndHotbarChanged += OnLayoutAndHotbarChanged;
        _cvars.LayoutChanged += ApplyLayout;
        _cvars.LabelsChanged += ApplyLabels;
        _cvars.Initialize();

        // Positioning is built after Initialize so it reads the now-loaded position CVars; its
        // lock/position reactions are non-immediate, matching the original subscriptions.
        _positioning = new ActionBarPositioningManager(UIManager, _cvars);
        _cvars.LockChanged += _positioning.RefreshDragHandleVisibility;
        _cvars.PositionChanged += _positioning.OnPositionChanged;

        _presets = new ActionBarPresetManager(_prefs, UIManager, _cvars, _positioning);

        // Seed the emote slots from the active preset before the actions system dispatches its
        // initial OnActionAdded burst, so emote actions can land on their saved slots even though
        // the full preset apply waits until HonkOnContainerReady has actions to bind.
        _presets.LoadActiveEmoteSlots();

        // Mirror the assign-hotkey toggle so the bar auto-reveals while the player rebinds slots.
        var slotHotkeys = UIManager.GetUIController<SlotHotkeyController>();
        ActionBarRuntimeConfig.Current.AssignHotkeyMode = slotHotkeys.AssignMode;
        slotHotkeys.AssignStateChanged += OnAssignStateChanged;
        // Rebuild the hotbar when any action-bar slot's binding changes so the labels track
        // what Settings → Controls currently holds.
        slotHotkeys.SlotBindingChanged += RefreshHotbar;
    }

    private void OnLayoutAndHotbarChanged()
    {
        ApplyLayout();
        RefreshHotbar();
    }

    private void OnAssignStateChanged()
    {
        var slotHotkeys = UIManager.GetUIController<SlotHotkeyController>();
        ActionBarRuntimeConfig.Current.AssignHotkeyMode = slotHotkeys.AssignMode;
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
            button.HighlightRect.Visible = ActionBarRuntimeConfig.Current.AssignHotkeyMode && armed == i;
            i++;
        }
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
        container.Columns = _cvars.SlotsPerRow;
        container.HSeparationOverride = _cvars.SlotSpacing;
        container.VSeparationOverride = _cvars.SlotSpacing;
        // Reveal every slot (pad up to rows x slots_per_row) when either the user's persistent
        // toggle is on, a drag is active, or the player is actively rebinding slot hotkeys.
        var runtime = ActionBarRuntimeConfig.Current;
        container.HonkMinSlotCount = runtime.ShowEmptySlots || runtime.IsDragActive || runtime.AssignHotkeyMode
            ? _cvars.Rows * _cvars.SlotsPerRow
            : 0;
    }

    private void ApplyLabels()
    {
        if (GetContainer() is not { } container)
            return;

        // Normally labels only render on slots with an action and on drag targets. Assign-hotkey
        // mode forces every slot's label visible so the player can see which key they're rebinding.
        var runtime = ActionBarRuntimeConfig.Current;
        foreach (var button in container.GetButtons())
        {
            button.Label.Visible = runtime.AssignHotkeyMode
                || (_cvars.ShowKeybindLabel && (button.Action != null || runtime.IsDragActive));
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

    // Wires the auto-add toggle and the Presets button on the actions window, called from the
    // upstream LoadGui (HONK) each time the window is (re)created. Lock has moved into the preset
    // window so the bar's customisation controls live in one place.
    public void HonkBindWindow(ActionsWindow window)
    {
        window.AutoAddButton.Pressed = ActionBarRuntimeConfig.Current.AutoAddActions;
        window.AutoAddButton.OnToggled += a => _cvars.SetAutoAddActions(a.Pressed);
        window.PresetsButton.OnPressed += _ => OpenPresetsWindow();
    }

    private void OpenPresetsWindow()
    {
        if (_presetsWindow is { Disposed: false })
        {
            _presetsWindow.Open();
            _presetsWindow.MoveToFront();
            return;
        }
        _presetsWindow = new ActionBarPresetsWindow(
            _presets.Store,
            _cfg,
            _presets.CapturePreset,
            _presets.ApplyPreset,
            _presets.ResetToDefaults,
            _presets.GetActiveCharacterName);
        _presetsWindow.OpenCentered();
    }

    // Called from the upstream ActionUIController (HONK) once the action bar widget is
    // registered. That's the first point the container reliably exists, so fresh client starts
    // get the stored layout applied here rather than relying on the order in which UIControllers
    // receive OnStateEntered.
    public void HonkOnContainerReady()
    {
        ApplyLayout();
        ApplyLabels();
        RefreshHotbar();
        _positioning.WireDragHandle();
        _positioning.ApplyPosition();
    }

    /// <summary>Auto-apply the first character-matched preset on first session start so a
    /// returning player gets their curated layout without clicking Load. Must run AFTER
    /// <c>LinkAllActions</c>: that call dispatches <c>OnActionAdded</c> for every linked action
    /// and appends them to the bar, which would clobber a preset applied earlier. Idempotent via
    /// the <see cref="_autoLoadedInitialPreset"/> flag, so subsequent screen reloads (e.g.
    /// respawn) don't trigger a second auto-load.</summary>
    public void HonkAfterInitialLink()
    {
        if (_autoLoadedInitialPreset)
            return;
        if (!UIManager.GetUIController<ActionUIController>().HonkHasClientActions())
            return;

        _autoLoadedInitialPreset = true;
        if (_presets.FindActivePresetForCharacter() is { } preset)
            _presets.ApplyPreset(preset);
    }

    // Called from the action drag hooks so the container can pad in empty drop targets and then
    // trim them back out once the drop completes.
    public void HonkSetDragActive(bool active)
    {
        if (ActionBarRuntimeConfig.Current.IsDragActive == active)
            return;
        ActionBarRuntimeConfig.Current.IsDragActive = active;
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

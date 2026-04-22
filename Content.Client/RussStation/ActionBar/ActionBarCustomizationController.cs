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
    public const bool OverridesRowLayout = true;

    // Base 0.0-1.0 alpha applied to every action button's slot background. Read each
    // frame by ActionButton.UpdateBackground (HONK block). The empty-slot fade scales
    // proportionally so the relative contrast stays consistent.
    public static float ButtonBackgroundAlpha { get; private set; } = 150f / 255f;

    // Flipped by ActionUIController drag hooks so empty drop targets are padded into the
    // container even when the persistent show-empty toggle is off.
    public static bool IsDragActive { get; private set; }

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
        container.HonkMinSlotCount = ShowEmptySlots || IsDragActive ? _rows * _slotsPerRow : 0;
    }

    private void ApplyLabels()
    {
        if (GetContainer() is not { } container)
            return;

        // Only reveal labels on slots that actually hold an action; empty buttons
        // shouldn't display a keybind that does nothing. Empty slots also show
        // labels during a drag so the player can see which slot maps to which key.
        foreach (var button in container.GetButtons())
        {
            button.Label.Visible = _showKeybindLabel && (button.Action != null || IsDragActive);
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

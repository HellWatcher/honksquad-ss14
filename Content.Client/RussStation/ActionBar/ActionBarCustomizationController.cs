using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.UserInterface.Systems.Actions.Widgets;
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
    private int _slotSpacing;
    private bool _showKeybindLabel;

    public override void Initialize()
    {
        base.Initialize();

        _rows = _cfg.GetCVar(CCVars.HonkActionBarRows);
        _slotSpacing = _cfg.GetCVar(CCVars.HonkActionBarSlotSpacing);
        _showKeybindLabel = _cfg.GetCVar(CCVars.HonkActionBarShowKeybindLabel);

        _cfg.OnValueChanged(CCVars.HonkActionBarRows, OnRowsChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarSlotSpacing, OnSlotSpacingChanged, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarShowKeybindLabel, OnShowKeybindLabelChanged, true);
    }

    private void OnRowsChanged(int value)
    {
        _rows = Math.Clamp(value, 1, 4);
        ApplyLayout();
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

    private ActionButtonContainer? GetContainer()
    {
        var bar = UIManager.GetActiveUIWidgetOrNull<ActionsBar>();
        return bar?.ActionsContainer;
    }

    private void ApplyLayout()
    {
        if (GetContainer() is not { } container)
            return;

        container.Rows = _rows;
        container.HSeparationOverride = _slotSpacing;
        container.VSeparationOverride = _slotSpacing;
    }

    private void ApplyLabels()
    {
        if (GetContainer() is not { } container)
            return;

        foreach (var button in container.GetButtons())
        {
            button.Label.Visible = _showKeybindLabel;
        }
    }

    public void OnStateEntered(GameplayState state)
    {
        ApplyLayout();
        ApplyLabels();
    }
}

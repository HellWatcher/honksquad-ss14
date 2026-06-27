using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client.RussStation.ActionBar;

// HONK Owns every CVar the fork action bar reads or writes: the initial reads, the change
// subscriptions, and the bulk writes used by preset apply / reset. The controller keeps a
// single reference and reacts to the change events below, replacing the seven near-identical
// OnValueChanged registrations and the field-by-field SetCVar block it used to inline.
public sealed class ActionBarCVarManager
{
    private readonly IConfigurationManager _cfg;

    // Clamped layout values the controller's ApplyLayout / ApplyLabels read when it rebuilds
    // the bar. Seeded by the invokeImmediately subscriptions in Initialize, so the explicit
    // up-front reads the controller used to do are folded into those callbacks.
    public int Rows { get; private set; }
    public int SlotsPerRow { get; private set; }
    public int SlotSpacing { get; private set; }
    public bool ShowKeybindLabel { get; private set; }

    // Last seen free-position coordinates. A negative value on either axis means "anchored".
    public float PositionX { get; private set; }
    public float PositionY { get; private set; }

    // Re-apply hooks, one per distinct reaction the original per-CVar handlers performed.
    public event Action? LayoutAndHotbarChanged;
    public event Action? LayoutChanged;
    public event Action? LabelsChanged;
    public event Action? LockChanged;
    public event Action? PositionChanged;

    public ActionBarCVarManager(IConfigurationManager cfg)
    {
        _cfg = cfg;
    }

    public void Initialize()
    {
        // Layout CVars: clamp into the supported range and re-apply on change. invokeImmediately
        // seeds the stored value, so no separate up-front GetCVar is needed.
        _cfg.OnValueChanged(CCVars.HonkActionBarRows, value =>
        {
            Rows = Math.Clamp(value, 1, 4);
            LayoutAndHotbarChanged?.Invoke();
        }, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarSlotsPerRow, value =>
        {
            SlotsPerRow = Math.Clamp(value, 1, 10);
            LayoutAndHotbarChanged?.Invoke();
        }, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarSlotSpacing, value =>
        {
            SlotSpacing = Math.Clamp(value, 0, 16);
            LayoutChanged?.Invoke();
        }, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarShowKeybindLabel, value =>
        {
            ShowKeybindLabel = value;
            LabelsChanged?.Invoke();
        }, true);

        // Cross-module flags read on per-frame hot paths: mirror straight into the shared
        // runtime config so ActionButton / ActionUIController stay off a UIController lookup.
        _cfg.OnValueChanged(CCVars.HonkActionBarShowEmptySlots, value =>
        {
            ActionBarRuntimeConfig.Current.ShowEmptySlots = value;
            LayoutAndHotbarChanged?.Invoke();
        }, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarAutoAddActions,
            value => ActionBarRuntimeConfig.Current.AutoAddActions = value, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarLock, value =>
        {
            ActionBarRuntimeConfig.Current.LockActions = value;
            LockChanged?.Invoke();
        }, true);
        _cfg.OnValueChanged(CCVars.HonkActionBarButtonBackgroundAlpha,
            value => ActionBarRuntimeConfig.Current.ButtonBackgroundAlpha = Math.Clamp(value, 0f, 1f), true);

        // Position CVars: seeded with an explicit read (no immediate apply — the widget isn't
        // up yet) then re-applied whenever they change.
        PositionX = _cfg.GetCVar(CCVars.HonkActionBarPositionX);
        PositionY = _cfg.GetCVar(CCVars.HonkActionBarPositionY);
        _cfg.OnValueChanged(CCVars.HonkActionBarPositionX, value => { PositionX = value; PositionChanged?.Invoke(); }, false);
        _cfg.OnValueChanged(CCVars.HonkActionBarPositionY, value => { PositionY = value; PositionChanged?.Invoke(); }, false);
    }

    public void SetAutoAddActions(bool value)
        => _cfg.SetCVar(CCVars.HonkActionBarAutoAddActions, value);

    // Persist whatever the in-flight drag landed on. SaveToFile so a crash mid-session doesn't
    // lose the player's chosen layout.
    public void WritePosition(float x, float y)
    {
        _cfg.SetCVar(CCVars.HonkActionBarPositionX, x);
        _cfg.SetCVar(CCVars.HonkActionBarPositionY, y);
        _cfg.SaveToFile();
    }

    public void ResetPosition()
    {
        _cfg.SetCVar(CCVars.HonkActionBarPositionX, CCVars.HonkActionBarPositionX.DefaultValue);
        _cfg.SetCVar(CCVars.HonkActionBarPositionY, CCVars.HonkActionBarPositionY.DefaultValue);
        _cfg.SaveToFile();
    }

    // Write every preset field back to its CVar in one shot, replacing the seven-plus inline
    // SetCVar calls the controller used to carry in ApplyPreset.
    public void WritePreset(ActionBarPreset preset)
    {
        _cfg.SetCVar(CCVars.HonkActionBarRows, preset.Rows);
        _cfg.SetCVar(CCVars.HonkActionBarSlotsPerRow, preset.SlotsPerRow);
        _cfg.SetCVar(CCVars.HonkActionBarSlotSpacing, preset.SlotSpacing);
        _cfg.SetCVar(CCVars.HonkActionBarShowKeybindLabel, preset.ShowKeybindLabel);
        _cfg.SetCVar(CCVars.HonkActionBarShowEmptySlots, preset.ShowEmptySlots);
        _cfg.SetCVar(CCVars.HonkActionBarAutoAddActions, preset.AutoAddActions);
        // Force lock on after every preset apply: a freshly-loaded curated layout shouldn't
        // get nudged by mis-clicks before the player has even looked at it. Players can still
        // toggle the lock back off from the presets window.
        _cfg.SetCVar(CCVars.HonkActionBarLock, true);
        _cfg.SetCVar(CCVars.HonkActionBarButtonBackgroundAlpha, preset.ButtonBackgroundAlpha);
        _cfg.SetCVar(CCVars.HonkActionBarPositionX, preset.PositionX);
        _cfg.SetCVar(CCVars.HonkActionBarPositionY, preset.PositionY);
        _cfg.SaveToFile();
    }
}

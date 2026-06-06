namespace Content.Client.RussStation.ActionBar;

// HONK Cross-module runtime configuration for the fork action bar. Populated at startup
// by ActionBarCVarManager and kept current as CVars / drag / assign-mode change, it is
// read on per-frame hot paths by ActionButton and ActionUIController. Bundling what used
// to be eight static read-before-init properties on the UIController into one struct makes
// the shared surface explicit and keeps those reads off a UIController lookup.
public struct ActionBarRuntimeConfig
{
    // Read by ActionButton.UpdateBackground (HONK block) on every frame so empty slots can
    // render a faint outline.
    public bool ShowEmptySlots;

    // Read by ActionUIController.OnActionAdded (HONK guard) so newly granted actions can
    // skip auto-population when the user wants a curated bar layout.
    public bool AutoAddActions;

    // Read by ActionUIController drag / right-click paths (HONK guards) to keep the bar
    // immutable when the player has locked the layout.
    public bool LockActions;

    // Base 0.0-1.0 alpha applied to every action button's slot background. Read each frame
    // by ActionButton.UpdateBackground (HONK block); the empty-slot fade scales proportionally
    // so the relative contrast stays consistent.
    public float ButtonBackgroundAlpha;

    // Flipped by the action drag hooks so empty drop targets are padded into the container
    // even when the persistent show-empty toggle is off.
    public bool IsDragActive;

    // Mirrored from SlotHotkeyController so the bar-side code (UpdateBackground, ApplyLabels)
    // can reveal every slot and its keybind label while the player is assigning hotkeys.
    public bool AssignHotkeyMode;

    // Read by the gameplay-screen resize handlers (HONK guards) to keep them from calling
    // MaxGridHeight/MaxGridWidth, which would flip the grid into size-limit mode and silently
    // overwrite the user's explicit row count on resize. Deliberately static readonly rather
    // than const: the game-screen call sites guard with `if (OverridesRowLayout) return;`, and a
    // compile-time const would make the following resize code unreachable (CS0162, treated as an
    // error here). A runtime value keeps that code reachable, matching the original field.
    public static readonly bool OverridesRowLayout = true;

    // The single live instance every reader and writer shares. Defaults match the original
    // static property initializers so behaviour before the first CVar load is unchanged.
    public static ActionBarRuntimeConfig Current = new()
    {
        AutoAddActions = true,
        ButtonBackgroundAlpha = 150f / 255f,
    };
}

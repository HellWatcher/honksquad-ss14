using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Number of rows the action bar's hotbar page is laid out over.
    /// The 10 hotbar slots reflow across this many rows; values outside
    /// 1-4 are clamped by the options UI.
    /// </summary>
    public static readonly CVarDef<int> HonkActionBarRows =
        CVarDef.Create("honk.action_bar.rows", 1, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Slots displayed per row. Combined with <see cref="HonkActionBarRows"/>
    /// this also determines how many empty slots get drawn when the empty-slot
    /// toggle is on; total is capped at the number of hotbar key bindings (20).
    /// </summary>
    public static readonly CVarDef<int> HonkActionBarSlotsPerRow =
        CVarDef.Create("honk.action_bar.slots_per_row", 10, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Pixel gap between adjacent action bar slots, applied as both
    /// horizontal and vertical separation on the hotbar container.
    /// </summary>
    public static readonly CVarDef<int> HonkActionBarSlotSpacing =
        CVarDef.Create("honk.action_bar.slot_spacing", 0, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to show the per-slot keybind label in the corner of each action button.
    /// </summary>
    public static readonly CVarDef<bool> HonkActionBarShowKeybindLabel =
        CVarDef.Create("honk.action_bar.show_keybind_label", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether to draw a faint outline on empty action bar slots so the layout
    /// is visible without every slot being populated. Empty slots go fully
    /// opaque while an action is being dragged so the drop targets stand out.
    /// </summary>
    public static readonly CVarDef<bool> HonkActionBarShowEmptySlots =
        CVarDef.Create("honk.action_bar.show_empty_slots", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether newly granted actions auto-populate the action bar. Off leaves the
    /// action available in the actions menu but keeps the bar layout untouched,
    /// so players curating a specific loadout don't have to re-remove every
    /// implant/species action that gets handed to them.
    /// </summary>
    public static readonly CVarDef<bool> HonkActionBarAutoAddActions =
        CVarDef.Create("honk.action_bar.auto_add_actions", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// When set, blocks drag-rearrangement and right-click-clear on the action bar
    /// so a curated layout can't be nudged by mis-clicks. Toggled from the actions
    /// window header button.
    /// </summary>
    public static readonly CVarDef<bool> HonkActionBarLock =
        CVarDef.Create("honk.action_bar.lock", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Base opacity of the slot background on each action bar button (0.0-1.0).
    /// Default matches the upstream hard-coded 150/255. Lower values make the
    /// bar fade into the background for low-vision or minimalist HUD preferences.
    /// The empty-slot preview fade scales from this value.
    /// </summary>
    public static readonly CVarDef<float> HonkActionBarButtonBackgroundAlpha =
        CVarDef.Create("honk.action_bar.button_background_alpha", 150f / 255f, CVar.CLIENTONLY | CVar.ARCHIVE);
}

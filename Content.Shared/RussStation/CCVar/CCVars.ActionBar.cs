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
}

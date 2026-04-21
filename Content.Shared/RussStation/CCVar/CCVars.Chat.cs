using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// When true, pressing the focus-chat keybind opens a floating text input anchored
    /// above the local player's sprite instead of focusing the HUD-corner chat box.
    /// Discards typed text on Escape; submits on Enter.
    /// </summary>
    public static readonly CVarDef<bool> FloatingChatInput =
        CVarDef.Create("honk.chat.floating_input", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}

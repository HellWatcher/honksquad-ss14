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

    /// <summary>
    /// When true alongside <see cref="FloatingChatInput"/>, the floating input reopens on
    /// the channel that was last used instead of defaulting to Local every time.
    /// </summary>
    public static readonly CVarDef<bool> FloatingChatInputRememberChannel =
        CVarDef.Create("honk.chat.floating_input_remember_channel", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Persists the last channel selected from the floating input when
    /// <see cref="FloatingChatInputRememberChannel"/> is on. Stored as the integer flag
    /// value of <c>ChatSelectChannel</c>.
    /// </summary>
    public static readonly CVarDef<int> FloatingChatInputLastChannel =
        CVarDef.Create("honk.chat.floating_input_last_channel", 0, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Persists the last <c>RadioChannelPrototype</c> ID used on the Radio channel when
    /// <see cref="FloatingChatInputRememberChannel"/> is on. Empty means "default/common"
    /// (or the prototype is missing and the widget falls back to common).
    /// </summary>
    public static readonly CVarDef<string> FloatingChatInputLastRadioChannel =
        CVarDef.Create("honk.chat.floating_input_last_radio_channel", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);
}

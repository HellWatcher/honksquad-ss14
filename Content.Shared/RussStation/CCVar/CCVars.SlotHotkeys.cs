using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Semicolon-separated list of <c>slotIndex=keyFunctionName</c> pairs mapping
    /// action-bar slots to arbitrary hotbar key functions. Lets slots past the
    /// fixed Hotbar0-Hotbar9 range (i.e. rows past the first) be reachable by
    /// keyboard, and lets the user remap which hotbar key fires which slot.
    /// </summary>
    /// <remarks>
    /// Example: <c>10=Hotbar1;11=Hotbar2</c>. Unknown key-function names are
    /// dropped at parse time. Slots with no explicit entry fall back to
    /// <c>Hotbar{(slot+1) % 10}</c> when <c>slot &lt; 10</c>, else unbound.
    /// </remarks>
    public static readonly CVarDef<string> HonkActionBarSlotHotkeys =
        CVarDef.Create("honk.action_bar.slot_hotkeys", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);
}

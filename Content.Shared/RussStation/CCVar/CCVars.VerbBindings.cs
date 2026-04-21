using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Semicolon-separated list of <c>slotIndex=emoteProtoId</c> pairs for the
    /// per-account verb bindings system (#579). Empty by default. The UI writes
    /// this CVar; the execution controller parses it on change.
    /// </summary>
    /// <remarks>
    /// Example: <c>0=Wave;3=Clap;7=Salute</c>. Slot indices must match the 20
    /// <c>HonkVerbBindN</c> key functions. Pairs with unknown prototype IDs
    /// are silently dropped at execution time.
    /// </remarks>
    public static readonly CVarDef<string> HonkVerbBindingSlots =
        CVarDef.Create("honk.verb_bindings.slots", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);
}

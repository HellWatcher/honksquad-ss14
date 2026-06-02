using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Master toggle for the botany overhaul. When enabled, hydroponics trays receive overhaul
    ///     growth behavior at map init (see <c>BotanyOverhaulSystem</c>) and the mutation algorithm
    ///     is fully replaced by <c>BotanyMutationOverhaulSystem</c> (random mutation rolls and
    ///     crossbreeding). Off (the default) runs vanilla botany unchanged.
    /// </summary>
    public static readonly CVarDef<bool> BotanyOverhaulEnabled =
        CVarDef.Create("botany.overhaul_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);
}

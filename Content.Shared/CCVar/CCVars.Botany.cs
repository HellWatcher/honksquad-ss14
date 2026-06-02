using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Proof-of-concept master toggle for the botany overhaul.
    ///     When enabled, hydroponics trays receive overhaul behavior at map init
    ///     (see <c>BotanyOverhaulSystem</c>) instead of running vanilla growth unchanged.
    ///     Defaults to off so the fork ships with stock botany until the overhaul is ready.
    /// </summary>
    public static readonly CVarDef<bool> BotanyOverhaulEnabled =
        CVarDef.Create("botany.overhaul_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);
}

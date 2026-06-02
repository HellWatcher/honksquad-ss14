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

    /// <summary>
    ///     Fully replaces the vanilla mutation algorithm (random mutation rolls and crossbreeding)
    ///     with <c>BotanyMutationOverhaulSystem</c> when enabled. The stock <c>MutationSystem</c>
    ///     delegates to the overhaul behind this flag; off (the default) runs vanilla unchanged.
    ///     Independent from <see cref="BotanyOverhaulEnabled"/> so the growth and genetics overhauls
    ///     can be toggled separately.
    /// </summary>
    public static readonly CVarDef<bool> BotanyMutationOverhaulEnabled =
        CVarDef.Create("botany.mutation_overhaul_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);
}

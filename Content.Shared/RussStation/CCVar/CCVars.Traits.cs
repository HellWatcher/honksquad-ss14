using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Global point budget shared across all trait categories.
    /// Negative-cost traits refund points, positive-cost traits spend them.
    /// </summary>
    public static readonly CVarDef<int> MaxTraitPoints =
        CVarDef.Create("game.max_trait_points", 10, CVar.REPLICATED | CVar.SERVER);
}

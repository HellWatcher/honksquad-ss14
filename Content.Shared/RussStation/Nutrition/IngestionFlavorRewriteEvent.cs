using Content.Shared.Chemistry.Components;

namespace Content.Shared.RussStation.Nutrition;

/// <summary>
/// Fork hook raised on the eater inside <c>IngestionSystem.OnEdibleIngested</c>
/// after the upstream flavor message is built and before it is rendered into the
/// eat / force-feed popup. Subscribers may overwrite <see cref="Flavors"/> to
/// substitute a different string (e.g. a skillchip-driven readout).
///
/// First consumer is the DET.ekt skillchip (#719), which replaces taste
/// descriptions with reagent names for <c>detective_taste</c> carriers. Other
/// chips or traits that want to rebind the popup's taste line can subscribe
/// without IngestionSystem learning about each of them.
/// </summary>
/// <param name="Eater">Mob doing the eating (target in force-feed).</param>
/// <param name="Solution">Reagent split being ingested this bite.</param>
[ByRefEvent]
public record struct IngestionFlavorRewriteEvent(EntityUid Eater, Solution Solution, string Flavors)
{
    /// <summary>
    /// Mutable. Starts at the upstream-built flavor string; subscribers may
    /// replace it. Last writer wins (no ordering between subscribers today).
    /// </summary>
    public string Flavors = Flavors;
}

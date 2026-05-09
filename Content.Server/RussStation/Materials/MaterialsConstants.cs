namespace Content.Server.RussStation.Materials;

/// <summary>
/// Fork-specific constants for the produce-to-material extractor (biogenerator).
/// </summary>
public static class MaterialsConstants
{
    // Issue #647: 3x lifts a baseline tomato harvest (~3 biomass) to ~9, which buys
    // exactly one Left4Zed print and lets potency-grinding stay an upgrade rather
    // than a prerequisite. 2x is the cautious floor; 4x makes biomass near-free.
    public const float ProduceExtractorYieldMultiplier = 3f;
}

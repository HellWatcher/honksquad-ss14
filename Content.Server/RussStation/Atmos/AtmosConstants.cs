using Content.Shared.Atmos;

namespace Content.Server.RussStation.Atmos;

/// <summary>
///     Fork-specific atmospheric constants for gas reactions.
/// </summary>
public static class AtmosConstants
{
    public const float HydrogenBurnRateDelta = 2f;
    public const float HydrogenOxygenFullburn = 10f;

    // Hydrogen burns hot but not as hot as tritium. Upstream's FireHydrogenEnergyReleased
    // (2.84 MJ/mol) is tuned for the rate-limited tritium reaction and would nuke tiles here.
    public const float HydrogenFireEnergyReleased = 200000f;

    public const float BZFormationMaxTemp = Atmospherics.T0C + 40f;
    public const float BZFormationRate = 0.4f;
    public const float BZFormationReactantRatioCap = 1f;

    public const float PluoxiumFormationMinTemp = 50f;
    public const float PluoxiumFormationMaxTemp = 273f;
    public const float PluoxiumFormationMaxRate = 5f;

    public const float HalonOxygenAbsorptionMinTemp = Atmospherics.T0C + 70f;
    public const float HalonOxygenAbsorptionRatio = 20f;

    public const float HealiumFormationMinTemp = 25f;
    public const float HealiumFormationMaxTemp = 300f;
    public const float HealiumFormationEnergy = 9000f;

    public const float NitriumFormationMinTemp = 1500f;
    public const float NitriumFormationTempDivisor = Atmospherics.FireMinimumTemperatureToExist * 8f;
    public const float NitriumFormationEnergy = 100000f;

    public const float NitriumDecompositionMaxTemp = Atmospherics.T0C + 70f;
    public const float NitriumDecompositionTempDivisor = Atmospherics.FireMinimumTemperatureToExist * 8f;
    public const float NitriumDecompositionEnergy = 30000f;

    public const float ProtoNitrateFormationMinTemp = 5000f;
    public const float ProtoNitrateFormationMaxTemp = 10000f;
    public const float ProtoNitrateFormationEnergy = 650f;

    public const float ProtoNitrateHydrogenConversionThreshold = 150f;
    public const float ProtoNitrateHydrogenConversionMaxRate = 5f;
    public const float ProtoNitrateHydrogenConversionEnergy = 2500f;

    public const float ProtoNitrateTritiumConversionMinTemp = 150f;
    public const float ProtoNitrateTritiumConversionMaxTemp = 340f;
    public const float ProtoNitrateTritiumConversionEnergy = 10000f;

    public const float ProtoNitrateBZDecompositionMinTemp = 260f;
    public const float ProtoNitrateBZDecompositionMaxTemp = 280f;
    public const float ProtoNitrateBZDecompositionEnergy = 60000f;

    public const float BZFormationPlasmaN2ORatioThreshold = 3f;
    public const float BZFormationN2ODecomposeNitrogenRatio = 0.5f;
    public const float BZFormationN2ODecomposeOxygenRatio = 0.5f;
    public const float BZFormationEnergyReleased = 80000f;

    public const float HalonFireMinTempScale = 10f;
    public const float HalonOxygenAbsorptionEnergy = 2500f;

    public const float HealiumHeatEfficiencyScale = 0.3f;
    public const float HealiumFrezonConsumedPerUnit = 2.75f;
    public const float HealiumBZConsumedPerUnit = 0.25f;
    public const float HealiumProducedPerUnit = 3f;

    public const float HydrogenFireOxygenBurnMultiplier = 2f;
    public const float HydrogenFireOxygenConsumedPerUnit = 0.5f;

    public const float NitriumFormationBZConsumedPerUnit = 0.05f;

    public const float PluoxiumOxygenConsumedPerUnit = 0.5f;
    public const float PluoxiumTritiumConsumedPerUnit = 0.01f;
    public const float PluoxiumFormationEnergy = 250f;

    public const float ProtoNitrateBZTempDivisor = 2240f;
    public const float ProtoNitrateBZNitrogenProducedPerUnit = 0.4f;
    public const float ProtoNitrateBZHeliumProducedPerUnit = 1.6f;
    public const float ProtoNitrateBZPlasmaProducedPerUnit = 0.8f;

    public const float ProtoNitrateFormationHeatEfficiencyScale = 0.005f;
    public const float ProtoNitrateFormationPluoxiumConsumedPerUnit = 0.2f;
    public const float ProtoNitrateFormationHydrogenConsumedPerUnit = 2f;
    public const float ProtoNitrateFormationProducedPerUnit = 2.2f;

    public const float ProtoNitrateHydrogenConversionProducedPerUnit = 0.5f;

    public const float ProtoNitrateTritiumTempDivisor = 34f;
    public const float ProtoNitrateTritiumProtoNitrateRatioWeight = 10f;
    public const float ProtoNitrateTritiumProtoNitrateConsumedPerUnit = 0.01f;

    // Radiation blob gradient math: bisect blob extent into a radius (half-width)
    // and subtract the flat-gradient baseline so slope==0 when min==max rads.
    public const float RadiationBlobInclusiveExtentAdjust = 1f;
    public const float RadiationBlobRadiusDivisor = 2f;
    public const float RadiationBlobFlatGradientBaseline = 1f;
}

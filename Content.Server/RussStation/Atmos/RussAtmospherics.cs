using Content.Shared.Atmos;

namespace Content.Server.RussStation.Atmos;

/// <summary>
///     Fork-specific atmospheric constants for gas reactions.
/// </summary>
public static class RussAtmospherics
{
    public const float MiasmaOxidationMinTemp = Atmospherics.T0C + 170f;
    public const float MiasmaOxidationRate = 5f;
    public const float MiasmaOxidationEnergyReleased = 100f;

    public const float HydrogenBurnRateDelta = 2f;
    public const float HydrogenOxygenFullburn = 10f;

    public const float BZFormationMaxTemp = Atmospherics.T0C + 40f;
    public const float BZFormationRate = 0.4f;

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
}

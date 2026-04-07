using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;

namespace Content.Server.RussStation.Atmos.Reactions;

/// <summary>
///     Shared helpers for fork gas reactions.
/// </summary>
public static class ReactionHelper
{
    /// <summary>
    ///     Applies an energy change to a gas mixture after moles have been adjusted.
    ///     Positive energy = exothermic (heats up), negative = endothermic (cools down).
    ///     Temperature is clamped to TCMB minimum for endothermic reactions.
    /// </summary>
    public static void AdjustEnergy(
        GasMixture mixture,
        AtmosphereSystem atmosphereSystem,
        float oldHeatCapacity,
        float energy,
        float heatScale,
        float? baseTemperature = null)
    {
        energy /= heatScale;
        var temp = baseTemperature ?? mixture.Temperature;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((temp * oldHeatCapacity + energy) / newHeatCapacity, Atmospherics.TCMB);
    }
}

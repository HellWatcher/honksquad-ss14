using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

/// <summary>
///     Miasma sterilization: high-temperature decomposition of miasma into oxygen.
///     Blocked when water vapor ratio exceeds 10% (humidity prevents sterilization).
/// </summary>
[UsedImplicitly]
public sealed partial class MiasmaOxidationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var totalMoles = mixture.TotalMoles;
        if (totalMoles <= 0)
            return ReactionResult.NoReaction;

        // Humidity blocks sterilization
        var waterVaporRatio = mixture.GetMoles(Gas.WaterVapor) / totalMoles;
        if (waterVaporRatio > 0.1f)
            return ReactionResult.NoReaction;

        var miasma = mixture.GetMoles(Gas.Miasma);
        var cleanedMoles = Math.Min(miasma, RussAtmospherics.MiasmaOxidationRate);

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Miasma, -cleanedMoles);
        mixture.AdjustMoles(Gas.Oxygen, cleanedMoles);

        var energyReleased = cleanedMoles * RussAtmospherics.MiasmaOxidationEnergyReleased;
        energyReleased /= heatScale;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = (mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;

        return ReactionResult.Reacting;
    }
}

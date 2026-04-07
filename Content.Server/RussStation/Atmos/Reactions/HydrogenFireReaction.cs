using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

/// <summary>
///     Hydrogen combustion: H2 + 0.5 O2 -> WaterVapor. Exothermic fire reaction.
/// </summary>
[UsedImplicitly]
public sealed partial class HydrogenFireReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var energyReleased = 0f;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var temperature = mixture.Temperature;
        var location = holder as TileAtmosphere;
        mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;

        var initialH2 = mixture.GetMoles(Gas.Hydrogen);
        var initialO2 = mixture.GetMoles(Gas.Oxygen);

        // burned = min(h2 / BURN_RATE_DELTA, o2 / (BURN_RATE_DELTA * OXYGEN_FULLBURN), h2, o2 * 2)
        var burned = Math.Min(
            initialH2 / RussAtmospherics.HydrogenBurnRateDelta,
            initialO2 / (RussAtmospherics.HydrogenBurnRateDelta * RussAtmospherics.HydrogenOxygenFullburn));
        burned = Math.Min(burned, initialH2);
        burned = Math.Min(burned, initialO2 * 2f);

        if (burned <= 0)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Hydrogen, -burned);
        mixture.AdjustMoles(Gas.Oxygen, -burned * 0.5f);
        mixture.AdjustMoles(Gas.WaterVapor, burned);

        energyReleased += burned * Atmospherics.FireHydrogenEnergyReleased;
        mixture.ReactionResults[(byte)GasReaction.Fire] += burned;

        if (energyReleased > 0)
            ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity, energyReleased, heatScale, temperature);

        if (location != null)
        {
            temperature = mixture.Temperature;
            if (temperature > Atmospherics.FireMinimumTemperatureToExist)
            {
                atmosphereSystem.HotspotExpose(location, temperature, mixture.Volume);
            }
        }

        return mixture.ReactionResults[(byte)GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
    }
}

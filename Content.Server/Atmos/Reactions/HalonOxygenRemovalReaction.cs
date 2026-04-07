using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

/// <summary>
///     Halon fire suppression: removes oxygen from the atmosphere and produces CO2.
///     1 Halon removes up to 20 O2. Endothermic (cools the area).
/// </summary>
[UsedImplicitly]
public sealed partial class HalonOxygenRemovalReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        var halon = mixture.GetMoles(Gas.Halon);
        var o2 = mixture.GetMoles(Gas.Oxygen);

        var heatEfficiency = Math.Min(
            temperature / (Atmospherics.FireMinimumTemperatureToExist * 10f),
            Math.Min(halon, o2 / Atmospherics.HalonOxygenAbsorptionRatio));

        if (heatEfficiency <= 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Halon, -heatEfficiency);
        mixture.AdjustMoles(Gas.Oxygen, -heatEfficiency * Atmospherics.HalonOxygenAbsorptionRatio);
        mixture.AdjustMoles(Gas.CarbonDioxide, heatEfficiency * Atmospherics.HalonOxygenAbsorptionRatio);

        var energyAbsorbed = heatEfficiency * 2500f;
        energyAbsorbed /= heatScale;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((temperature * oldHeatCapacity - energyAbsorbed) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}

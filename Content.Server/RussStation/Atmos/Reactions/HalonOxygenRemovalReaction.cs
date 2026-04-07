using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

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
            Math.Min(halon, o2 / RussAtmospherics.HalonOxygenAbsorptionRatio));

        if (heatEfficiency <= 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Halon, -heatEfficiency);
        mixture.AdjustMoles(Gas.Oxygen, -heatEfficiency * RussAtmospherics.HalonOxygenAbsorptionRatio);
        mixture.AdjustMoles(Gas.CarbonDioxide, heatEfficiency * RussAtmospherics.HalonOxygenAbsorptionRatio);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            -(heatEfficiency * 2500f), heatScale, temperature);

        return ReactionResult.Reacting;
    }
}

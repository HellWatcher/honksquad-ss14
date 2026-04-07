using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

/// <summary>
///     Pluoxium formation: CO2 + 0.5 O2 + 0.01 Tritium -> Pluoxium + 0.01 Hydrogen.
///     Cold synthesis (50K-273K), max rate 5 mol/tick.
/// </summary>
[UsedImplicitly]
public sealed partial class PluoxiumFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var co2 = mixture.GetMoles(Gas.CarbonDioxide);
        var o2 = mixture.GetMoles(Gas.Oxygen);
        var tritium = mixture.GetMoles(Gas.Tritium);

        // Rate limited by all three reactants and max rate
        var produced = Math.Min(co2, o2 / 0.5f);
        produced = Math.Min(produced, tritium / 0.01f);
        produced = Math.Min(produced, Atmospherics.PluoxiumFormationMaxRate);

        if (produced <= 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.CarbonDioxide, -produced);
        mixture.AdjustMoles(Gas.Oxygen, -produced * 0.5f);
        mixture.AdjustMoles(Gas.Tritium, -produced * 0.01f);
        mixture.AdjustMoles(Gas.Pluoxium, produced);
        mixture.AdjustMoles(Gas.Hydrogen, produced * 0.01f);

        var energyReleased = produced * 250f;
        energyReleased /= heatScale;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = (mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;

        return ReactionResult.Reacting;
    }
}

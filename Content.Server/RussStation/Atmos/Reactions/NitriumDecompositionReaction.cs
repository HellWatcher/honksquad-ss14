using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

/// <summary>
///     Nitrium decomposition: Nitrium -> Hydrogen + Nitrogen in the presence of O2. Exothermic.
///     Aggressively slow, limited by temperature. Only occurs below ~343K.
/// </summary>
[UsedImplicitly]
public sealed partial class NitriumDecompositionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        var nitrium = mixture.GetMoles(Gas.Nitrium);

        var heatEfficiency = Math.Min(
            temperature / AtmosConstants.NitriumDecompositionTempDivisor,
            nitrium);

        if (heatEfficiency <= 0 || nitrium - heatEfficiency < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Nitrium, -heatEfficiency);
        mixture.AdjustMoles(Gas.Hydrogen, heatEfficiency);
        mixture.AdjustMoles(Gas.Nitrogen, heatEfficiency);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            heatEfficiency * AtmosConstants.NitriumDecompositionEnergy, heatScale, temperature);

        return ReactionResult.Reacting;
    }
}

using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

/// <summary>
///     Nitrium formation: Tritium + Nitrogen + 0.05 BZ -> Nitrium. Endothermic.
///     Requires high temperature (1500K+). BZ is consumed slowly as catalyst.
/// </summary>
[UsedImplicitly]
public sealed partial class NitriumFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        var tritium = mixture.GetMoles(Gas.Tritium);
        var nitrogen = mixture.GetMoles(Gas.Nitrogen);
        var bz = mixture.GetMoles(Gas.BZ);

        var heatEfficiency = Math.Min(
            temperature / AtmosConstants.NitriumFormationTempDivisor,
            Math.Min(tritium, Math.Min(nitrogen, bz / AtmosConstants.NitriumFormationBZConsumedPerUnit)));

        if (heatEfficiency <= 0
            || tritium - heatEfficiency < 0
            || nitrogen - heatEfficiency < 0
            || bz - heatEfficiency * AtmosConstants.NitriumFormationBZConsumedPerUnit < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Tritium, -heatEfficiency);
        mixture.AdjustMoles(Gas.Nitrogen, -heatEfficiency);
        mixture.AdjustMoles(Gas.BZ, -heatEfficiency * AtmosConstants.NitriumFormationBZConsumedPerUnit);
        mixture.AdjustMoles(Gas.Nitrium, heatEfficiency);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            -(heatEfficiency * AtmosConstants.NitriumFormationEnergy), heatScale, temperature);

        return ReactionResult.Reacting;
    }
}

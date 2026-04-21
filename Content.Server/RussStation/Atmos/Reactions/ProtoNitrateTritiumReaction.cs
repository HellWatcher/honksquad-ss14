using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

/// <summary>
///     Proto-nitrate converts tritium into hydrogen (de-irradiation).
///     Consumes a small amount of proto-nitrate as catalyst.
///     Exothermic.
/// </summary>
[UsedImplicitly]
public sealed partial class ProtoNitrateTritiumReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        var tritium = mixture.GetMoles(Gas.Tritium);
        var protoNitrate = mixture.GetMoles(Gas.ProtoNitrate);

        // Rate scales with temperature and reactant ratio, capped by available moles.
        var producedAmount = Math.Min(
            temperature / AtmosConstants.ProtoNitrateTritiumTempDivisor
                * (tritium * protoNitrate)
                / (tritium + AtmosConstants.ProtoNitrateTritiumProtoNitrateRatioWeight * protoNitrate),
            Math.Min(tritium, protoNitrate / AtmosConstants.ProtoNitrateTritiumProtoNitrateConsumedPerUnit));

        if (producedAmount <= 0
            || tritium - producedAmount < 0
            || protoNitrate - producedAmount * AtmosConstants.ProtoNitrateTritiumProtoNitrateConsumedPerUnit < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.ProtoNitrate, -producedAmount * AtmosConstants.ProtoNitrateTritiumProtoNitrateConsumedPerUnit);
        mixture.AdjustMoles(Gas.Tritium, -producedAmount);
        mixture.AdjustMoles(Gas.Hydrogen, producedAmount);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            producedAmount * AtmosConstants.ProtoNitrateTritiumConversionEnergy, heatScale);

        return ReactionResult.Reacting;
    }
}

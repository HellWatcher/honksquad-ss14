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
            temperature / 34f * (tritium * protoNitrate) / (tritium + 10f * protoNitrate),
            Math.Min(tritium, protoNitrate / 0.01f));

        if (producedAmount <= 0
            || tritium - producedAmount < 0
            || protoNitrate - producedAmount * 0.01f < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.ProtoNitrate, -producedAmount * 0.01f);
        mixture.AdjustMoles(Gas.Tritium, -producedAmount);
        mixture.AdjustMoles(Gas.Hydrogen, producedAmount);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            producedAmount * RussAtmospherics.ProtoNitrateTritiumConversionEnergy, heatScale);

        return ReactionResult.Reacting;
    }
}

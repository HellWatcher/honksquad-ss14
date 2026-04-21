using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

/// <summary>
///     Proto-nitrate decomposes BZ into nitrogen, helium, and plasma.
///     Exothermic. Operates in a narrow temperature band (260-280K).
/// </summary>
[UsedImplicitly]
public sealed partial class ProtoNitrateBZReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        var bz = mixture.GetMoles(Gas.BZ);
        var protoNitrate = mixture.GetMoles(Gas.ProtoNitrate);

        var consumedAmount = Math.Min(
            temperature / AtmosConstants.ProtoNitrateBZTempDivisor * bz * protoNitrate / (bz + protoNitrate),
            Math.Min(bz, protoNitrate));

        if (consumedAmount <= 0 || bz - consumedAmount < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.BZ, -consumedAmount);
        mixture.AdjustMoles(Gas.Nitrogen, consumedAmount * AtmosConstants.ProtoNitrateBZNitrogenProducedPerUnit);
        mixture.AdjustMoles(Gas.Helium, consumedAmount * AtmosConstants.ProtoNitrateBZHeliumProducedPerUnit);
        mixture.AdjustMoles(Gas.Plasma, consumedAmount * AtmosConstants.ProtoNitrateBZPlasmaProducedPerUnit);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            consumedAmount * AtmosConstants.ProtoNitrateBZDecompositionEnergy, heatScale);

        return ReactionResult.Reacting;
    }
}

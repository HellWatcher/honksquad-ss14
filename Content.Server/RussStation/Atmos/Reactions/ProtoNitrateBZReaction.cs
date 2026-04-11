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
            temperature / 2240f * bz * protoNitrate / (bz + protoNitrate),
            Math.Min(bz, protoNitrate));

        if (consumedAmount <= 0 || bz - consumedAmount < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.BZ, -consumedAmount);
        mixture.AdjustMoles(Gas.Nitrogen, consumedAmount * 0.4f);
        mixture.AdjustMoles(Gas.Helium, consumedAmount * 1.6f);
        mixture.AdjustMoles(Gas.Plasma, consumedAmount * 0.8f);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            consumedAmount * RussAtmospherics.ProtoNitrateBZDecompositionEnergy, heatScale);

        return ReactionResult.Reacting;
    }
}

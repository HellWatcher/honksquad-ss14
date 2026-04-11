using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ProtoNitrateHydrogenReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var hydrogen = mixture.GetMoles(Gas.Hydrogen);
        var protoNitrate = mixture.GetMoles(Gas.ProtoNitrate);

        var producedAmount = Math.Min(RussAtmospherics.ProtoNitrateHydrogenConversionMaxRate,
            Math.Min(hydrogen, protoNitrate));

        if (producedAmount <= 0 || hydrogen - producedAmount < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Hydrogen, -producedAmount);
        mixture.AdjustMoles(Gas.ProtoNitrate, producedAmount * 0.5f);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            -(producedAmount * RussAtmospherics.ProtoNitrateHydrogenConversionEnergy), heatScale, mixture.Temperature);

        return ReactionResult.Reacting;
    }
}

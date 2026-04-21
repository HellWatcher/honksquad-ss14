using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ProtoNitrateFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        var pluoxium = mixture.GetMoles(Gas.Pluoxium);
        var hydrogen = mixture.GetMoles(Gas.Hydrogen);

        var heatEfficiency = Math.Min(temperature * RussAtmospherics.ProtoNitrateFormationHeatEfficiencyScale,
            Math.Min(pluoxium / RussAtmospherics.ProtoNitrateFormationPluoxiumConsumedPerUnit,
                hydrogen / RussAtmospherics.ProtoNitrateFormationHydrogenConsumedPerUnit));

        if (heatEfficiency <= 0
            || pluoxium - heatEfficiency * RussAtmospherics.ProtoNitrateFormationPluoxiumConsumedPerUnit < 0
            || hydrogen - heatEfficiency * RussAtmospherics.ProtoNitrateFormationHydrogenConsumedPerUnit < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Pluoxium, -heatEfficiency * RussAtmospherics.ProtoNitrateFormationPluoxiumConsumedPerUnit);
        mixture.AdjustMoles(Gas.Hydrogen, -heatEfficiency * RussAtmospherics.ProtoNitrateFormationHydrogenConsumedPerUnit);
        mixture.AdjustMoles(Gas.ProtoNitrate, heatEfficiency * RussAtmospherics.ProtoNitrateFormationProducedPerUnit);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            heatEfficiency * RussAtmospherics.ProtoNitrateFormationEnergy, heatScale);

        return ReactionResult.Reacting;
    }
}

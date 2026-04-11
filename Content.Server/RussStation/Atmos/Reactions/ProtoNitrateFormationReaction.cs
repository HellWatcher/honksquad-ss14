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

        var heatEfficiency = Math.Min(temperature * 0.005f,
            Math.Min(pluoxium / 0.2f, hydrogen / 2f));

        if (heatEfficiency <= 0 || pluoxium - heatEfficiency * 0.2f < 0 || hydrogen - heatEfficiency * 2f < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Pluoxium, -heatEfficiency * 0.2f);
        mixture.AdjustMoles(Gas.Hydrogen, -heatEfficiency * 2f);
        mixture.AdjustMoles(Gas.ProtoNitrate, heatEfficiency * 2.2f);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            heatEfficiency * RussAtmospherics.ProtoNitrateFormationEnergy, heatScale);

        return ReactionResult.Reacting;
    }
}

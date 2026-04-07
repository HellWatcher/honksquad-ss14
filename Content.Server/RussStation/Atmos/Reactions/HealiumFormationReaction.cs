using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

/// <summary>
///     Healium formation: 2.75 Frezon + 0.25 BZ -> 3 Healium. Exothermic.
///     Rate scales with temperature (25K-300K range).
/// </summary>
[UsedImplicitly]
public sealed partial class HealiumFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        var frezon = mixture.GetMoles(Gas.Frezon);
        var bz = mixture.GetMoles(Gas.BZ);

        var heatEfficiency = Math.Min(temperature * 0.3f,
            Math.Min(frezon / 2.75f, bz / 0.25f));

        if (heatEfficiency <= 0 || frezon - heatEfficiency * 2.75f < 0 || bz - heatEfficiency * 0.25f < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Frezon, -heatEfficiency * 2.75f);
        mixture.AdjustMoles(Gas.BZ, -heatEfficiency * 0.25f);
        mixture.AdjustMoles(Gas.Healium, heatEfficiency * 3f);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            heatEfficiency * RussAtmospherics.HealiumFormationEnergy, heatScale);

        return ReactionResult.Reacting;
    }
}

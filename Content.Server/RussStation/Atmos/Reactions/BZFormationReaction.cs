using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.RussStation.Atmos.Reactions;

/// <summary>
///     BZ formation from N2O and Plasma at cold temperatures.
///     If plasma:N2O ratio > 3:1, N2O decomposes instead (no BZ formed).
/// </summary>
[UsedImplicitly]
public sealed partial class BZFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var n2o = mixture.GetMoles(Gas.NitrousOxide);
        var plasma = mixture.GetMoles(Gas.Plasma);

        if (n2o <= 0)
            return ReactionResult.NoReaction;

        // If plasma:N2O ratio > 3:1, N2O decomposes into N2+O2 instead
        if (plasma / n2o > 3f)
        {
            mixture.AdjustMoles(Gas.NitrousOxide, -n2o);
            mixture.AdjustMoles(Gas.Nitrogen, n2o * 0.5f);
            mixture.AdjustMoles(Gas.Oxygen, n2o * 0.5f);
            return ReactionResult.Reacting;
        }

        var ratio = Math.Min(n2o / plasma, 1f);
        var produced = Math.Min(n2o * RussAtmospherics.BZFormationRate * ratio,
            plasma * RussAtmospherics.BZFormationRate * ratio);

        if (produced <= 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.NitrousOxide, -produced);
        mixture.AdjustMoles(Gas.Plasma, -produced);
        mixture.AdjustMoles(Gas.BZ, produced);

        ReactionHelper.AdjustEnergy(mixture, atmosphereSystem, oldHeatCapacity,
            produced * 80000f, heatScale);

        return ReactionResult.Reacting;
    }
}

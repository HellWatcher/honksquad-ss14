// HONK — Issue #647: produce-to-biomass yield was anchored to upstream 1u Nutriment = 1 biomass,
// which left botany's biogenerator one print short of every harvest. Adding a fork-side yield
// multiplier here (partial extension on the existing component) keeps the change off the upstream
// file and avoids any [Access] friction since reads happen inside the owning system.

using Content.Server.RussStation.Materials;

namespace Content.Server.Materials.Components;

public sealed partial class ProduceMaterialExtractorComponent
{
    /// <summary>
    /// Multiplier applied to extracted biomass before flooring to int. Default in
    /// <see cref="MaterialsConstants.ProduceExtractorYieldMultiplier"/>.
    /// </summary>
    [DataField]
    public float YieldMultiplier = MaterialsConstants.ProduceExtractorYieldMultiplier;
}

using Content.Server.Botany;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.RussStation.Botany;
using Content.Shared.Slippery;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Botany.Systems;

/// <summary>
///     Serializes <see cref="SeedData"/> into a <see cref="PlantAnalyzerUiState"/> for the plant analyzer UI.
/// </summary>
public static class SeedDataFormatter
{
    /// <summary>
    ///     Builds the full UI state shown by the plant analyzer for the given seed.
    /// </summary>
    public static PlantAnalyzerUiState FormatSeedData(
        SeedData seed,
        EntityUid target,
        IEntityManager entityManager,
        IPrototypeManager prototypeManager,
        SharedAtmosphereSystem atmosphere,
        ISawmill log)
    {
        var chemicals = new Dictionary<string, FixedPoint2>();
        foreach (var (reagentId, q) in seed.Chemicals)
        {
            if (!prototypeManager.TryIndex<ReagentPrototype>(reagentId, out var reagent))
            {
                log.Warning($"PlantAnalyzer: reagent '{reagentId}' not found for seed '{seed.Name}'.");
                continue;
            }

            chemicals[reagent.LocalizedName] = CalculateChemicalAmount(q, seed.Potency);
        }

        return new PlantAnalyzerUiState
        {
            SeedName = Loc.GetString(seed.DisplayName),
            Lifespan = seed.Lifespan,
            Maturation = seed.Maturation,
            Production = seed.Production,
            Yield = seed.Yield,
            Potency = seed.Potency,
            GrowthStages = seed.GrowthStages,
            HarvestRepeat = FormatHarvestRepeat(seed.HarvestRepeat, log),
            Endurance = seed.Endurance,
            IdealLight = seed.IdealLight,
            WaterConsumption = seed.WaterConsumption,
            NutrientConsumption = seed.NutrientConsumption,
            IdealHeat = seed.IdealHeat,
            HeatTolerance = seed.HeatTolerance,
            LightTolerance = seed.LightTolerance,
            ToxinsTolerance = seed.ToxinsTolerance,
            LowPressureTolerance = seed.LowPressureTolerance,
            HighPressureTolerance = seed.HighPressureTolerance,
            PestTolerance = seed.PestTolerance,
            WeedTolerance = seed.WeedTolerance,
            Traits = EnumerateSeedTraits(seed, target, entityManager),
            Chemicals = chemicals,
            ConsumeGases = BuildGasDict(seed.ConsumeGasses, atmosphere, log),
            ExudeGases = BuildGasDict(seed.ExudeGasses, atmosphere, log),
        };
    }

    /// <summary>
    ///     Computes the amount of a chemical a seed produces: the minimum plus a potency-scaled
    ///     bonus, clamped between the chemical's configured minimum and maximum.
    /// </summary>
    public static FixedPoint2 CalculateChemicalAmount(SeedChemQuantity quantity, float potency)
    {
        var amount = quantity.Min;
        if (quantity.PotencyDivisor > 0 && potency > 0)
            amount += potency / quantity.PotencyDivisor;
        return FixedPoint2.Clamp(amount, quantity.Min, quantity.Max);
    }

    /// <summary>
    ///     Maps a <see cref="HarvestType"/> to its localized description.
    /// </summary>
    public static string FormatHarvestRepeat(HarvestType harvestRepeat, ISawmill log)
    {
        string key;
        switch (harvestRepeat)
        {
            case HarvestType.NoRepeat:
                key = "plant-analyzer-harvest-no-repeat";
                break;
            case HarvestType.Repeat:
                key = "plant-analyzer-harvest-repeat";
                break;
            case HarvestType.SelfHarvest:
                key = "plant-analyzer-harvest-self-harvest";
                break;
            default:
                log.Warning($"PlantAnalyzer: unrecognized HarvestType {harvestRepeat}.");
                key = "plant-analyzer-harvest-no-repeat";
                break;
        }

        return Loc.GetString(key);
    }

    /// <summary>
    ///     Collects the localized trait descriptions for a seed, including traits derived from
    ///     components on the scanned entity.
    /// </summary>
    public static List<string> EnumerateSeedTraits(SeedData seed, EntityUid target, IEntityManager entityManager)
    {
        var traits = new List<string>();

        if (seed.Seedless)
            traits.Add(Loc.GetString("plant-analyzer-trait-seedless"));
        if (!seed.Viable)
            traits.Add(Loc.GetString("plant-analyzer-trait-unviable"));
        if (seed.Ligneous)
            traits.Add(Loc.GetString("plant-analyzer-trait-ligneous"));
        if (seed.TurnIntoKudzu)
            traits.Add(Loc.GetString("plant-analyzer-trait-kudzufication"));
        if (seed.CanScream)
            traits.Add(Loc.GetString("plant-analyzer-trait-screaming"));
        if (entityManager.HasComponent<GhostTakeoverAvailableComponent>(target))
            traits.Add(Loc.GetString("plant-analyzer-trait-sentient"));
        if (entityManager.HasComponent<SlipperyComponent>(target))
            traits.Add(Loc.GetString("plant-analyzer-trait-slippery"));
        if (seed.SplatPrototype != null)
            traits.Add(Loc.GetString("plant-analyzer-trait-splatter"));

        return traits;
    }

    private static Dictionary<string, float> BuildGasDict(Dictionary<Gas, float> gases, SharedAtmosphereSystem atmosphere, ISawmill log)
    {
        var result = new Dictionary<string, float>();
        foreach (var (gas, rate) in gases)
        {
            var proto = atmosphere.GetGas(gas);
            if (proto == null)
            {
                log.Warning($"PlantAnalyzer: unknown gas ID {(int)gas}, skipping.");
                continue;
            }
            result[Loc.GetString(proto.Name)] = rate;
        }
        return result;
    }
}

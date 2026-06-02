using Content.Server.Botany;
using Content.Shared.Atmos;
using Content.Shared.EntityEffects;
using Content.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.RussStation.Botany.Systems;

/// <summary>
///     Full replacement for the vanilla mutation algorithm, gated behind
///     <c>botany.overhaul_enabled</c>. When the CVar is on, the stock
///     <see cref="MutationSystem"/> delegates <c>MutateSeed</c> and <c>Cross</c> to the methods
///     here; when off, vanilla runs unchanged.
///
///     The overhaul swaps vanilla's "coin-flip each stat from one parent" genetics for a
///     continuous blend model: offspring stats interpolate between both parents with random
///     variance and occasional hybrid vigor, hybrids inherit the union of both parents' chemicals,
///     gasses and mutations, and random mutation rolls additionally apply severity-scaled numeric
///     drift so every mutation tick nudges the genome.
/// </summary>
public sealed class BotanyMutationOverhaulSystem : EntitySystem
{
    private static readonly ProtoId<RandomPlantMutationListPrototype> RandomPlantMutations = "RandomPlantMutations";

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffects = default!;

    private RandomPlantMutationListPrototype _mutations = default!;

    public override void Initialize()
    {
        base.Initialize();
        _mutations = _prototype.Index(RandomPlantMutations);
    }

    /// <summary>
    ///     Overhauled random mutation: rolls prototype effects with a severity-curved probability
    ///     and additionally drifts a couple of numeric stats so every mutation tick nudges the genome.
    /// </summary>
    public void MutateSeed(EntityUid plantHolder, ref SeedData seed, float severity)
    {
        if (!seed.Unique)
        {
            Log.Error("Attempted to mutate a shared seed");
            return;
        }

        // Severity curve: low severity barely mutates, high severity ramps up fast.
        var chanceScale = MathF.Pow(Math.Clamp(severity / 25f, 0f, 1f), 0.5f);

        foreach (var mutation in _mutations.mutations)
        {
            if (!_random.Prob(Math.Min(mutation.BaseOdds * chanceScale, 1.0f)))
                continue;

            if (mutation.AppliesToPlant)
                _entityEffects.TryApplyEffect(plantHolder, mutation.Effect);

            // Stat-adjusting effects don't persist; only flagged mutations stick to the genome.
            if (mutation.Persists && seed.Mutations.All(m => m.Name != mutation.Name))
                seed.Mutations.Add(mutation);
        }

        // Continuous genetic drift unique to the overhaul.
        var drift = Math.Clamp(severity / 25f, 0f, 1f);
        seed.Potency = Math.Clamp(seed.Potency + RandomDrift(drift) * 5f, 0f, 100f);
        seed.Yield = Math.Max(0, seed.Yield + (int)MathF.Round(RandomDrift(drift)));
    }

    /// <summary>
    ///     Overhauled crossbreeding: offspring blend continuously between both parents rather than
    ///     inheriting whole stats from one. Chemicals, gasses and mutations from both parents are
    ///     inherited, making hybrids richer than vanilla.
    /// </summary>
    public SeedData Cross(SeedData a, SeedData b)
    {
        var result = b.Clone();

        BlendFloat(ref result.NutrientConsumption, a.NutrientConsumption);
        BlendFloat(ref result.WaterConsumption, a.WaterConsumption);
        BlendFloat(ref result.IdealHeat, a.IdealHeat);
        BlendFloat(ref result.HeatTolerance, a.HeatTolerance);
        BlendFloat(ref result.IdealLight, a.IdealLight);
        BlendFloat(ref result.LightTolerance, a.LightTolerance);
        BlendFloat(ref result.ToxinsTolerance, a.ToxinsTolerance);
        BlendFloat(ref result.LowPressureTolerance, a.LowPressureTolerance);
        BlendFloat(ref result.HighPressureTolerance, a.HighPressureTolerance);
        BlendFloat(ref result.PestTolerance, a.PestTolerance);
        BlendFloat(ref result.WeedTolerance, a.WeedTolerance);

        BlendFloat(ref result.Endurance, a.Endurance);
        BlendInt(ref result.Yield, a.Yield);
        BlendFloat(ref result.Lifespan, a.Lifespan);
        BlendFloat(ref result.Maturation, a.Maturation);
        BlendFloat(ref result.Production, a.Production);
        BlendFloat(ref result.Potency, a.Potency);

        // Bools can't be blended; keep the vanilla coin-flip for these.
        CrossBool(ref result.Seedless, a.Seedless);
        CrossBool(ref result.Ligneous, a.Ligneous);
        CrossBool(ref result.TurnIntoKudzu, a.TurnIntoKudzu);
        CrossBool(ref result.CanScream, a.CanScream);

        // Hybrids inherit the union of both parents' chemicals and gasses.
        UnionChemicals(result.Chemicals, a.Chemicals);
        UnionGasses(result.ExudeGasses, a.ExudeGasses);
        UnionGasses(result.ConsumeGasses, a.ConsumeGasses);

        // Carry every mutation from both parents forward, deduped by name.
        result.Mutations = result.Mutations
            .UnionBy(a.Mutations, m => m.Name)
            .ToList();

        return result;
    }

    /// <summary>
    ///     Interpolate between the current value and the other parent's value by a random factor,
    ///     with a small chance of hybrid vigor / regression that nudges the result past the blend.
    /// </summary>
    private void BlendFloat(ref float val, float other)
    {
        var t = _random.NextFloat();
        var blended = val + (other - val) * t;

        if (_random.Prob(0.15f))
            blended *= 1f + (_random.NextFloat() - 0.5f) * 0.2f;

        val = blended;
    }

    private void BlendInt(ref int val, int other)
    {
        var t = _random.NextFloat();
        val = (int)MathF.Round(val + (other - val) * t);
    }

    private void CrossBool(ref bool val, bool other)
    {
        val = _random.Prob(0.5f) ? val : other;
    }

    private void UnionChemicals(Dictionary<string, SeedChemQuantity> target, Dictionary<string, SeedChemQuantity> other)
    {
        foreach (var (key, value) in other)
        {
            if (target.ContainsKey(key))
                continue;

            // Inherited (non-inherent) chemicals are stripped on species mutation, matching vanilla.
            var chem = value;
            chem.Inherent = false;
            target[key] = chem;
        }
    }

    private void UnionGasses(Dictionary<Gas, float> target, Dictionary<Gas, float> other)
    {
        foreach (var (key, value) in other)
            target.TryAdd(key, value);
    }

    /// <summary>
    ///     Returns a value in [-scale, scale].
    /// </summary>
    private float RandomDrift(float scale)
    {
        return (_random.NextFloat() - 0.5f) * 2f * scale;
    }
}

using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityConditions;
using Content.Shared.EntityConditions.Conditions;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.EntityEffects.Effects.Solution;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.EntityEffects.Effects.Transform;
using Content.Shared.FixedPoint;
using Content.Shared.RussStation.MedicalScanner;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.MedicalScanner;

/// <summary>
/// Reagent-analysis engine pulled out of <see cref="HealthAnalyzerReagentSystem"/>. Walks a
/// reagent's metabolisms, classifies each self-gated effect as harmful or beneficial, and
/// buckets the gating bounds into overdose/underdose thresholds.
///
/// Living in its own system makes the classifier reusable by other tooling (chem scanners,
/// pharmacist tools) and independently testable. Results are memoized per reagent in
/// <see cref="_thresholdCache"/>, which is dropped whenever reagent prototypes reload.
/// </summary>
public sealed class ReagentDoseThresholdAnalyzer : EntitySystem
{
    private readonly Dictionary<string, ReagentDoseThresholds> _thresholdCache = new();

    public readonly record struct ReagentDoseThresholds(
        FixedPoint2? HarmfulMin,
        FixedPoint2? HarmfulMax,
        FixedPoint2? BeneficialMin);

    private enum EffectClass { Harmful, Beneficial, Neutral }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReagentPrototype>())
            _thresholdCache.Clear();
    }

    /// <summary>
    /// Walks a reagent's metabolisms looking for self-referencing <see cref="ReagentCondition"/>s
    /// and buckets the bounds into harmful or beneficial thresholds based on the effect type.
    /// </summary>
    public ReagentDoseThresholds GetDoseThresholds(ReagentPrototype proto)
    {
        if (_thresholdCache.TryGetValue(proto.ID, out var cached))
            return cached;

        FixedPoint2? harmfulMin = null;
        FixedPoint2? harmfulMax = null;
        FixedPoint2? beneficialMin = null;

        if (proto.Metabolisms != null)
        {
            foreach (var (_, entry) in proto.Metabolisms.Metabolisms)
            {
                foreach (var effect in entry.Effects)
                {
                    if (effect.Conditions == null)
                        continue;

                    var cls = ClassifyEffect(effect, proto.ID);
                    if (cls == EffectClass.Neutral)
                        continue;

                    var (selfMin, selfMax) = SelfBounds(proto.ID, effect.Conditions);
                    if (selfMin is null && selfMax is null)
                        continue;

                    if (cls == EffectClass.Beneficial)
                    {
                        if (selfMin is { } bMin && (beneficialMin is null || bMin < beneficialMin.Value))
                            beneficialMin = bMin;
                    }
                    else
                    {
                        if (selfMin is { } hMin && (harmfulMin is null || hMin < harmfulMin.Value))
                            harmfulMin = hMin;
                        if (selfMax is { } hMax && (harmfulMax is null || hMax > harmfulMax.Value))
                            harmfulMax = hMax;
                    }
                }
            }
        }

        var result = new ReagentDoseThresholds(harmfulMin, harmfulMax, beneficialMin);
        _thresholdCache[proto.ID] = result;
        return result;
    }

    private static (FixedPoint2? Min, FixedPoint2? Max) SelfBounds(string reagentId, EntityCondition[] conditions)
    {
        FixedPoint2? min = null;
        FixedPoint2? max = null;
        foreach (var cond in conditions)
        {
            if (cond is not ReagentCondition rc)
                continue;
            if (rc.Reagent != reagentId)
                continue;
            if (rc.Inverted)
                continue;

            if (rc.Min > FixedPoint2.Zero && (min is null || rc.Min < min.Value))
                min = rc.Min;
            if (rc.Max < FixedPoint2.MaxValue && (max is null || rc.Max > max.Value))
                max = rc.Max;
        }
        return (min, max);
    }

    private static EffectClass ClassifyEffect(EntityEffect effect, string reagentId)
    {
        switch (effect)
        {
            case HealthChange hc:
                return ClassifyDamageValues(hc.Damage.DamageDict.Values);
            case EvenHealthChange ehc:
                return ClassifyDamageValues(ehc.Damage.Values);

            case AdjustReagent ar:
                if (ar.Reagent == reagentId)
                    return ar.Amount < FixedPoint2.Zero ? EffectClass.Neutral : EffectClass.Harmful;
                return EffectClass.Neutral;

            case MovementSpeedModifier msm:
                if (msm.WalkSpeedModifier < MedicalScannerConstants.NeutralMovementSpeedModifier
                    || msm.SprintSpeedModifier < MedicalScannerConstants.NeutralMovementSpeedModifier)
                    return EffectClass.Harmful;
                if (msm.WalkSpeedModifier > MedicalScannerConstants.NeutralMovementSpeedModifier
                    || msm.SprintSpeedModifier > MedicalScannerConstants.NeutralMovementSpeedModifier)
                    return EffectClass.Beneficial;
                return EffectClass.Neutral;

            case PopupMessage:
            case Emote:
            case GenericStatusEffect:
            case ModifyStatusEffect:
                return EffectClass.Neutral;

            default:
                return EffectClass.Harmful;
        }
    }

    private static EffectClass ClassifyDamageValues(IEnumerable<FixedPoint2> values)
    {
        var anyPositive = false;
        var anyNegative = false;
        foreach (var v in values)
        {
            if (v > FixedPoint2.Zero)
                anyPositive = true;
            else if (v < FixedPoint2.Zero)
                anyNegative = true;
        }
        if (anyPositive)
            return EffectClass.Harmful;
        if (anyNegative)
            return EffectClass.Beneficial;
        return EffectClass.Neutral;
    }
}

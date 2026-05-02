using Content.Shared.Body;
using Content.Shared.Emp;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Applies EMP effects to bodies that contain cybernetic organs.
/// </summary>
public sealed class CyberneticEmpSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId CardiacArrestEffect = "StatusEffectCardiacArrest";
    private static readonly EntProtoId BreathingSuppressedEffect = "StatusEffectBreathingSuppressed";
    private static readonly EntProtoId BlindnessEffect = "StatusEffectTemporaryBlindness";
    private static readonly EntProtoId DeafnessEffect = "StatusEffectTemporaryDeafness";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, EmpPulseEvent>(OnBodyEmpPulse);
    }

    private void OnBodyEmpPulse(EntityUid uid, BodyComponent body, ref EmpPulseEvent args)
    {
        if (body.Organs == null)
            return;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!TryComp<CyberneticOrganComponent>(organ, out var cyber))
                continue;

            args.Affected = true;
            ApplyEmpEffect(uid, cyber, args.Duration, args.EnergyConsumption);
        }
    }

    private void ApplyEmpEffect(
        EntityUid body,
        CyberneticOrganComponent cyber,
        TimeSpan baseDuration,
        float energy)
    {
        var duration = baseDuration * cyber.EmpVulnerability;

        var effectId = cyber.EmpEffect switch
        {
            CyberneticEmpEffect.CardiacArrest => (EntProtoId?) CardiacArrestEffect,
            CyberneticEmpEffect.BreathingFailure => BreathingSuppressedEffect,
            CyberneticEmpEffect.Flicker => BlindnessEffect,
            CyberneticEmpEffect.Deafen => DeafnessEffect,
            _ => null,
        };

        if (effectId == null)
            return;

        if (!_statusEffects.TryAddStatusEffectDuration(body, effectId.Value, duration))
            Log.Debug($"Failed to apply EMP effect {cyber.EmpEffect} to {ToPrettyString(body)}");
    }
}

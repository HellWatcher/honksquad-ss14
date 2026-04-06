using Content.Shared.Body;
using Content.Shared.Emp;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Applies EMP effects to bodies that contain cybernetic organs.
/// </summary>
public sealed class CyberneticEmpSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId CardiacArrestEffect = "StatusEffectCardiacArrest";
    private static readonly EntProtoId DrowsinessEffect = "StatusEffectDrowsiness";
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

        switch (cyber.EmpEffect)
        {
            case CyberneticEmpEffect.CardiacArrest:
                _statusEffects.TryAddStatusEffectDuration(body, CardiacArrestEffect, duration);
                break;

            case CyberneticEmpEffect.BreathingFailure:
                _stun.TryAddStunDuration(body, duration);
                break;

            case CyberneticEmpEffect.Flicker:
                _statusEffects.TryAddStatusEffectDuration(body, DrowsinessEffect, duration);
                break;

            case CyberneticEmpEffect.Deafen:
                _statusEffects.TryAddStatusEffectDuration(body, DeafnessEffect, duration);
                break;

            case CyberneticEmpEffect.None:
                break;
        }
    }
}

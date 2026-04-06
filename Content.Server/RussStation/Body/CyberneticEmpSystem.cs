using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
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
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private static readonly ProtoId<DamageTypePrototype> ShockDamageType = "Shock";
    private static readonly EntProtoId DrowsinessEffect = "StatusEffectDrowsiness";
    private static readonly EntProtoId SlurredEffect = "StatusEffectSlurred";

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
                var damage = new DamageSpecifier(_proto.Index(ShockDamageType), 30 * cyber.EmpVulnerability);
                _damageable.TryChangeDamage(body, damage);
                break;

            case CyberneticEmpEffect.BreathingFailure:
                _stun.TryAddStunDuration(body, duration);
                break;

            case CyberneticEmpEffect.Flicker:
                _statusEffects.TryAddStatusEffectDuration(body, DrowsinessEffect, duration);
                break;

            case CyberneticEmpEffect.Deafen:
                _statusEffects.TryAddStatusEffectDuration(body, SlurredEffect, duration);
                break;

            case CyberneticEmpEffect.None:
                break;
        }
    }
}

using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Bridges the new status effect system to upstream temporary blindness.
/// When the EMP blindness effect is applied, adds <see cref="TemporaryBlindnessComponent"/>
/// to the body so the upstream <see cref="TemporaryBlindnessSystem"/> handles the actual blinding.
/// </summary>
public sealed class EmpBlindnessSystem : EntitySystem
{
    [Dependency] private readonly BlindableSystem _blindable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmpBlindnessComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<EmpBlindnessComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<EmpBlindnessComponent> ent, ref StatusEffectAppliedEvent args)
    {
        EnsureComp<TemporaryBlindnessComponent>(args.Target);
    }

    private void OnRemoved(Entity<EmpBlindnessComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<TemporaryBlindnessComponent>(args.Target);
        _blindable.UpdateIsBlind(args.Target);
    }
}

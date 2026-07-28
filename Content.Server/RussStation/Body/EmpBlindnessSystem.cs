using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Bridges the fork's EMP blindness status effect to upstream's blindness implementation.
/// Upstream removed <c>TemporaryBlindnessComponent</c> (which used to be added to the body);
/// blindness is now expressed by <see cref="BlindnessStatusEffectComponent"/> sitting on the
/// status effect entity itself, with <see cref="BlindnessSystem"/> cancelling the relayed
/// <see cref="CanSeeAttemptEvent"/> for as long as the effect is active.
/// So this system tags the EMP effect entity with that component while the effect is applied.
/// Duration is still owned by the status effect, so the timing semantics are unchanged.
/// </summary>
public sealed partial class EmpBlindnessSystem : EntitySystem
{
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmpBlindnessComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<EmpBlindnessComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<EmpBlindnessComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        // Added during the applied event, so BlindnessSystem's own applied handler will not have
        // run for this effect entity. Refresh the target ourselves.
        EnsureComp<BlindnessStatusEffectComponent>(ent);
        _blindable.UpdateIsBlind(args.Target);
    }

    private void OnRemoved(Entity<EmpBlindnessComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<BlindnessStatusEffectComponent>(ent);
        _blindable.UpdateIsBlind(args.Target);
    }
}

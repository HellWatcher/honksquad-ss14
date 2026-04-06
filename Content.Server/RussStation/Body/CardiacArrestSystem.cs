using Content.Server.Body.Systems;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Handles the CardiacArrest status effect.
/// When active, stuns the target and rapidly drains respirator saturation
/// to simulate oxygen not reaching cells due to the heart not pumping blood.
/// Lungs still work, but can't keep up with the drain.
/// </summary>
public sealed class CardiacArrestSystem : EntitySystem
{
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    /// <summary>
    /// Extra saturation drained per second. Normal drain is ~1/s, so 3/s extra
    /// means lungs can't compensate and the entity suffocates.
    /// </summary>
    private const float DrainPerSecond = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardiacArrestComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<CardiacArrestComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<CardiacArrestComponent> ent, ref StatusEffectAppliedEvent args)
    {
        EnsureComp<ActiveCardiacArrestComponent>(args.Target);

        if (TryComp<StatusEffectComponent>(ent, out var effect))
            _stun.TryAddStunDuration(args.Target, effect.Duration);
    }

    private void OnRemoved(Entity<CardiacArrestComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<ActiveCardiacArrestComponent>(args.Target);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var drain = -DrainPerSecond * frameTime;
        var query = EntityQueryEnumerator<ActiveCardiacArrestComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            _respirator.UpdateSaturation(uid, drain);
        }
    }
}

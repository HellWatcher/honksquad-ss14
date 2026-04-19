// HONK - Break pulls when the two endpoints end up on different maps.
// Without this, FTL-reparenting the pullee (e.g. arrivals grid returning
// from the FTL map) leaves the pulling distance joint cross-map, which
// trips the engine's cross-map joint assert during client prediction.
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameObjects;

namespace Content.Shared.RussStation.Physics;

public sealed class PullMapGuardSystem : EntitySystem
{
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PullableComponent, EntParentChangedMessage>(OnPullableParentChanged);
        SubscribeLocalEvent<PullerComponent, EntParentChangedMessage>(OnPullerParentChanged);
    }

    private void OnPullableParentChanged(Entity<PullableComponent> ent, ref EntParentChangedMessage args)
    {
        if (ent.Comp.Puller is not { } puller)
            return;

        if (_transform.GetMapId(ent.Owner) == _transform.GetMapId(puller))
            return;

        _pulling.TryStopPull(ent.Owner, ent.Comp);
    }

    private void OnPullerParentChanged(Entity<PullerComponent> ent, ref EntParentChangedMessage args)
    {
        if (ent.Comp.Pulling is not { } pullable)
            return;

        if (_transform.GetMapId(ent.Owner) == _transform.GetMapId(pullable))
            return;

        if (!TryComp<PullableComponent>(pullable, out var pullableComp))
            return;

        _pulling.TryStopPull(pullable, pullableComp);
    }
}

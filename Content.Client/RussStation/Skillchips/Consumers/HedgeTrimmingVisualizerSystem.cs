using Content.Shared.RussStation.Skillchips.Consumers;
using Robust.Client.GameObjects;

namespace Content.Client.RussStation.Skillchips.Consumers;

/// <summary>
/// Applies the trimmed sprite state chosen server-side (see
/// <see cref="SharedHedgeTrimmingSystem"/>) to the plant's sprite when the
/// networked component state arrives.
/// </summary>
public sealed class HedgeTrimmingVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HedgeTrimmableComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, HedgeTrimmableComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (comp.CurrentState == null)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerSetRsiState((uid, sprite), 0, comp.CurrentState);
    }
}

using System.Numerics;
using Content.Shared.RussStation.Skillchips.Consumers.Kommand;
using Robust.Client.GameObjects;

namespace Content.Client.RussStation.Skillchips.Consumers.Kommand;

/// <summary>
/// Client visualizer for <see cref="KommandEnhancedArrowComponent"/>. When the
/// component lands on a freshly spawned PointingArrow, scale up the sprite and
/// tint it the holder's chosen color. Component state arrives via the standard
/// AutoNetworkedField sync, so subscribing to AfterAutoHandleState catches both
/// initial spawn-with-component and any color change from the BUI.
/// </summary>
public sealed class KommandClientSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<KommandEnhancedArrowComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<KommandEnhancedArrowComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnInit(Entity<KommandEnhancedArrowComponent> ent, ref ComponentInit args)
        => Apply(ent);

    private void OnAfterState(Entity<KommandEnhancedArrowComponent> ent, ref AfterAutoHandleStateEvent args)
        => Apply(ent);

    private void Apply(Entity<KommandEnhancedArrowComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        _sprite.SetScale((ent.Owner, sprite), new Vector2(ent.Comp.Scale, ent.Comp.Scale));
        _sprite.SetColor((ent.Owner, sprite), ent.Comp.Color);
    }
}

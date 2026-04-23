using Content.Shared.RussStation.Surgery.Components;
using Robust.Client.GameObjects;

namespace Content.Client.RussStation.Surgery;

/// <summary>
/// Mirrors the parent mob's sprite rotation onto the drape overlay each frame so the drape lays
/// down with a prone / buckled patient instead of standing upright. Direction picking is still left
/// to the engine's world-rotation-based RSI lookup.
/// </summary>
public sealed class SurgeryDrapeOverlayVisibilitySystem : EntitySystem
{
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<SurgeryDrapeOverlayVisibilityComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var overlaySprite, out var overlayXform))
        {
            var parent = overlayXform.ParentUid;
            if (!parent.IsValid() || !TryComp<SpriteComponent>(parent, out var parentSprite))
                continue;

            overlaySprite.Rotation = parentSprite.Rotation;
        }
    }
}

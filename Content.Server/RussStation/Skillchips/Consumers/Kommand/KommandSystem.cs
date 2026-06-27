using System.Numerics;
using Content.Server.Pointing.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.RussStation.Skillchips.Consumers.Kommand;
using Robust.Shared.Map;

namespace Content.Server.RussStation.Skillchips.Consumers.Kommand;

/// <summary>
/// Server-side Kommand. Inherits the shared lifecycle + BUI bookkeeping and
/// stamps every freshly spawned <see cref="PointingArrowComponent"/> with
/// <see cref="KommandEnhancedArrowComponent"/> carrying the pointer's chosen
/// color. The client visualizer reads that and tints + scales the arrow
/// sprite. SS13 parallel: <c>fancier_pointer</c> in
/// <c>code/modules/library/skill_learning/generic_skillchips/point.dm</c>.
///
/// We scan in <see cref="Update"/> rather than hooking
/// <c>AfterPointedAtEvent</c> because upstream <c>PointingSystem.TryPoint</c>
/// only raises that event in its entity-pointed branch; pointing at a bare
/// tile spawns the arrow but skips the event. Polling unstamped arrows
/// handles tile and entity points the same way without touching upstream.
/// </summary>
public sealed class KommandSystem : SharedKommandSystem
{
    /// <summary>
    /// Radius (in map units) around the recovered pointer position to look for
    /// a chip-holding mob. PointingSystem records the pointer's world position
    /// into the arrow at spawn; a mob moves well under a tile per tick, so half
    /// a tile is comfortably larger than any frame-of-flight skew.
    /// </summary>
    private const float PointerMatchRadiusSquared = 0.5f;

    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityQuery<KommandEnhancedArrowComponent> _enhancedQuery = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var arrowQuery = EntityQueryEnumerator<PointingArrowComponent>();
        while (arrowQuery.MoveNext(out var arrowUid, out var arrow))
        {
            if (_enhancedQuery.HasComponent(arrowUid))
                continue;

            // PointingSystem sets StartPosition and EndTime together after Spawn returns.
            // Until EndTime is non-zero the arrow is mid-init and StartPosition would
            // resolve to the arrow itself, which would erroneously match any chip holder
            // standing on the pointed tile.
            if (arrow.EndTime == TimeSpan.Zero)
                continue;

            // StartPosition is stored relative to the arrow itself (see PointingSystem.TryPoint:
            // _transform.ToCoordinates((arrow, Transform(arrow)), playerMapCoords).Position), so
            // the inverse rebuilds EntityCoordinates against the arrow uid, not its parent.
            var pointerCoords = _transform.ToMapCoordinates(new EntityCoordinates(arrowUid, arrow.StartPosition));

            EntityUid? best = null;
            var bestDistSq = PointerMatchRadiusSquared;
            var mobQuery = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
            while (mobQuery.MoveNext(out var mobUid, out _, out var mobXform))
            {
                if (!Skillchip.HasCapability(mobUid, EnhancedPointingTag))
                    continue;

                var mobCoords = _transform.GetMapCoordinates(mobUid, mobXform);
                if (mobCoords.MapId != pointerCoords.MapId)
                    continue;

                var dSq = Vector2.DistanceSquared(mobCoords.Position, pointerCoords.Position);
                if (dSq < bestDistSq)
                {
                    best = mobUid;
                    bestDistSq = dSq;
                }
            }

            if (best is not { } pointer)
                continue;

            // EnsureComp the preference here as well as on first picker open. A chip holder
            // who points before ever opening the picker should still get the default-red arrow.
            var pref = EnsureComp<KommandColorPreferenceComponent>(pointer);
            var enhanced = EnsureComp<KommandEnhancedArrowComponent>(arrowUid);
            enhanced.Color = pref.Color;
            Dirty(arrowUid, enhanced);
        }
    }
}

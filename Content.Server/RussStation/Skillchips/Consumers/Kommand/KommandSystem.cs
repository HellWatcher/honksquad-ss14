using System.Numerics;
using Content.Server.Pointing.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Pointing;
using Content.Shared.RussStation.Skillchips.Consumers.Kommand;
using Robust.Shared.Map;

namespace Content.Server.RussStation.Skillchips.Consumers.Kommand;

/// <summary>
/// Server-side Kommand. Inherits the shared lifecycle + BUI bookkeeping and
/// adds the actual point-time hook: when a Kommand chip holder points at
/// something, stamp the freshly spawned PointingArrow with
/// <see cref="KommandEnhancedArrowComponent"/> carrying the holder's chosen
/// color. The client visualizer reads that and tints + scales the arrow
/// sprite. SS13 parallel: <c>fancier_pointer</c> in
/// <c>code/modules/library/skill_learning/generic_skillchips/point.dm</c>.
/// </summary>
public sealed class KommandSystem : SharedKommandSystem
{
    /// <summary>
    /// Radius (in map units) around the pointed entity to look for an unstamped PointingArrow.
    /// PointingSystem spawns the arrow on the pointed-at tile, so anything within a tile is
    /// safely "this point's arrow"; concurrent pointers at the same target will share the
    /// bonus, which mirrors how the upstream arrow itself is non-attributable.
    /// </summary>
    private const float ArrowMatchRadiusSquared = 1.0f;

    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Hook every mob. AfterPointedAtEvent is only raised on pointers, so the
        // overhead of subscribing on a universal mob component is one dispatch per
        // actual point.
        SubscribeLocalEvent<MobStateComponent, AfterPointedAtEvent>(OnAfterPointed);
    }

    private void OnAfterPointed(Entity<MobStateComponent> mob, ref AfterPointedAtEvent args)
    {
        // Capability gate first: cheap rejection for the 99.9% of pointers who don't have the chip.
        if (!Skillchip.HasCapability(mob.Owner, EnhancedPointingTag))
            return;

        // EnsureComp the preference here too, not just on first picker open. A chip holder who
        // points before ever opening the picker should still get the default-red enhanced arrow.
        var pref = EnsureComp<KommandColorPreferenceComponent>(mob.Owner);

        var pointedCoords = _transform.GetMapCoordinates(args.Pointed);

        EntityUid? best = null;
        var bestDistSq = ArrowMatchRadiusSquared;
        var query = EntityQueryEnumerator<PointingArrowComponent, TransformComponent>();
        while (query.MoveNext(out var arrowUid, out _, out var xform))
        {
            if (HasComp<KommandEnhancedArrowComponent>(arrowUid))
                continue;

            var arrowCoords = _transform.GetMapCoordinates(arrowUid, xform);
            if (arrowCoords.MapId != pointedCoords.MapId)
                continue;

            var dSq = Vector2.DistanceSquared(arrowCoords.Position, pointedCoords.Position);
            if (dSq < bestDistSq)
            {
                best = arrowUid;
                bestDistSq = dSq;
            }
        }

        if (best is not { } arrow)
            return;

        var enhanced = EnsureComp<KommandEnhancedArrowComponent>(arrow);
        enhanced.Color = pref.Color;
        Dirty(arrow, enhanced);
    }
}

using Content.Shared.Movement.Systems;
using Content.Shared.RussStation.Wounds;
using Content.Shared.RussStation.Wounds.Systems;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Wounds;

public sealed class WoundSystem : SharedWoundSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WoundComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ActiveWounds.Count == 0)
                continue;

            var changed = false;
            for (var i = comp.ActiveWounds.Count - 1; i >= 0; i--)
            {
                var wound = comp.ActiveWounds[i];

                if (!_proto.TryIndex(wound.WoundTypeId, out var proto))
                    continue;

                if (proto.DegradationTime <= 0 || wound.Tier <= 1)
                    continue;

                wound.TimeAtCurrentTier += TimeSpan.FromSeconds(frameTime);

                if (wound.TimeAtCurrentTier.TotalSeconds < proto.DegradationTime)
                    continue;

                wound.Tier--;
                wound.TimeAtCurrentTier = TimeSpan.Zero;
                changed = true;
            }

            if (changed)
            {
                Dirty(uid, comp);
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }
        }
    }
}

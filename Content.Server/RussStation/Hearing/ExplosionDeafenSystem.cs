using Content.Shared.Explosion;
using Content.Shared.RussStation.Hearing;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Hearing;

/// <summary>
/// Anyone caught in an explosion takes a short bout of deafness on top of the damage,
/// matching the in-character expectation that being next to a bomb leaves your ears
/// ringing. Mirrors flashbang deafening (DeafenOnTriggerSystem) for the explosion path.
/// </summary>
public sealed class ExplosionDeafenSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    private static readonly EntProtoId TemporaryDeafnessEffect = "StatusEffectTemporaryDeafness";
    private static readonly TimeSpan DeafenDuration = TimeSpan.FromSeconds(8);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeafableComponent, BeforeExplodeEvent>(OnBeforeExplode);
    }

    private void OnBeforeExplode(EntityUid uid, DeafableComponent component, ref BeforeExplodeEvent args)
    {
        _status.TryAddStatusEffectDuration(uid, TemporaryDeafnessEffect, DeafenDuration);
    }
}

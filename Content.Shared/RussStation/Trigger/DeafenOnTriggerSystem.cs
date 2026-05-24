using Content.Shared.RussStation.Hearing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.RussStation.Trigger;

/// <summary>
/// Mirrors <see cref="Content.Shared.Trigger.Systems.FlashOnTriggerSystem"/> for the audio side:
/// when triggered, each entity in <see cref="DeafenOnTriggerComponent.Range"/> with a
/// <see cref="DeafableComponent"/> rolls <see cref="DeafenOnTriggerComponent.Probability"/> and
/// gets the StatusEffectTemporaryDeafness applied for the configured duration.
/// </summary>
public sealed class DeafenOnTriggerSystem : XOnTriggerSystem<DeafenOnTriggerComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    private static readonly EntProtoId TemporaryDeafnessEffect = "StatusEffectTemporaryDeafness";

    private readonly HashSet<Entity<DeafableComponent>> _hits = new();

    protected override void OnTrigger(Entity<DeafenOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        var coords = _xform.GetMapCoordinates(target);

        _hits.Clear();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Range, _hits);

        foreach (var hit in _hits)
        {
            if (!_random.Prob(ent.Comp.Probability))
                continue;

            _status.TryAddStatusEffectDuration(hit.Owner, TemporaryDeafnessEffect, ent.Comp.Duration);
        }

        args.Handled = true;
    }
}

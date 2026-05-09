// HONK - Issue #302 follow-up: reagent-driven random teleport for BluespaceDust.
// Reuses the same blink path as the crystal's UseInHand and ThrowDoHit so the
// VFX, audio, tile-blocked filter, and predicted RNG all match what players see
// when crushing a crystal in hand.

using Content.Shared.EntityEffects;
using Content.Shared.RussStation.Bluespace.EntitySystems;
using Robust.Shared.Prototypes;
using BluespaceConstants = Content.Shared.RussStation.Bluespace.BluespaceConstants;

namespace Content.Shared.RussStation.Bluespace.EntityEffects;

public sealed partial class BluespaceTeleportEntityEffectSystem
    : EntityEffectSystem<TransformComponent, BluespaceTeleport>
{
    [Dependency] private readonly BluespaceBlinkSystem _blink = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<BluespaceTeleport> args)
    {
        _blink.TryBlink(
            entity,
            args.Effect.Range * args.Scale,
            args.Effect.SourceEffect,
            args.Effect.DestinationEffect);
    }
}

public sealed partial class BluespaceTeleport : EntityEffectBase<BluespaceTeleport>
{
    /// <summary>
    /// Maximum random offset (in tiles) the teleport throws the drinker.
    /// Mirrors SS13's reagent default of 5 tiles, which is shorter than the
    /// crystal's 8-tile crush blink to keep the metabolism trigger weaker than
    /// the active use.
    /// </summary>
    [DataField]
    public float Range = BluespaceConstants.DefaultReagentBlinkRange;

    /// <summary>
    /// Visual effect spawned on the tile the drinker leaves. Defaults match
    /// the crystal blink so reagent and item teleports look identical.
    /// </summary>
    [DataField]
    public EntProtoId? SourceEffect = "EffectPhaseOut";

    /// <summary>
    /// Visual effect spawned on the tile the drinker arrives at.
    /// </summary>
    [DataField]
    public EntProtoId? DestinationEffect = "EffectPhaseIn";

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-bluespace-teleport", ("chance", Probability), ("range", Range));
}

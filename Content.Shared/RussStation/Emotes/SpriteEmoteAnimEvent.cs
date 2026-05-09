using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Emotes;

[Serializable, NetSerializable]
public enum SpriteEmoteKind : byte
{
    /// Continuous rotation of <c>SpriteComponent.Rotation</c> through <see cref="Turns"/> turns.
    Flip,
    /// Stepped cycle of the sprite's facing direction (N→E→S→W) over Duration.
    Spin,
}

/// HONK Server-to-client one-shot signal for fork emote sprite animations (Flip / Spin).
/// Sent as a network event rather than a networked component because AfterAutoHandleStateEvent
/// timing is fragile for short-lived runtime-added components and the client needs no further
/// state once the animation is queued.
[Serializable, NetSerializable]
public sealed class SpriteEmoteAnimEvent : EntityEventArgs
{
    public NetEntity Target;
    public SpriteEmoteKind Kind;
    public TimeSpan Duration;
    /// Total turns for <see cref="SpriteEmoteKind.Flip"/>; total cardinal-direction steps for <see cref="SpriteEmoteKind.Spin"/>.
    public float Amount;

    public SpriteEmoteAnimEvent(NetEntity target, SpriteEmoteKind kind, TimeSpan duration, float amount)
    {
        Target = target;
        Kind = kind;
        Duration = duration;
        Amount = amount;
    }
}

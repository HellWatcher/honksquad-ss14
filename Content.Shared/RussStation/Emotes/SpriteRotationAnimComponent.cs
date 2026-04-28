using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Emotes;

/// HONK Drives a one-shot client-side sprite rotation (Flip / Spin emote).
/// Server attaches the component, client plays the animation; server removes it after Duration.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpriteRotationAnimComponent : Component
{
    /// Total animation length.
    [DataField(required: true), AutoNetworkedField]
    public TimeSpan Duration;

    /// Number of full 360 degree rotations to play across Duration.
    [DataField, AutoNetworkedField]
    public float Turns = 1f;

    /// Server-only deadline at which the component is removed. Not networked because the
    /// client just needs Duration to size the animation.
    [DataField]
    public TimeSpan EndTime;
}

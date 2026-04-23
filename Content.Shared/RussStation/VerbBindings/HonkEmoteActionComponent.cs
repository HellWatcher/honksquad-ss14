using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.VerbBindings;

/// <summary>
/// HONK Marks an action entity whose trigger plays a specific emote as the performer.
/// Paired with <see cref="HonkEmoteActionEvent"/> on the action's <c>InstantActionComponent</c>
/// so the server emote system picks it up and dispatches through <c>ChatSystem.TryEmoteWithChat</c>.
/// Stored as a plain string (rather than ProtoId&lt;EmotePrototype&gt;) so the
/// auto-networking generator round-trips the value cleanly on every client.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HonkEmoteActionComponent : Component
{
    /// <summary>The emote proto id this action plays when fired. Overwritten by the server at
    /// spawn to the allowlisted emote this action was granted for.</summary>
    [DataField, AutoNetworkedField]
    public string Emote = string.Empty;
}

using Content.Shared.Chat.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.VerbBindings;

/// <summary>
/// HONK Marks an action entity whose trigger plays a specific emote as the performer.
/// Paired with <see cref="HonkEmoteActionEvent"/> on the action's <c>InstantActionComponent</c>
/// so the server emote system picks it up and dispatches through <c>ChatSystem.TryEmoteWithChat</c>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HonkEmoteActionComponent : Component
{
    /// <summary>The emote this action plays when fired. Overwritten by the server at spawn
    /// to the allowlisted emote this action was granted for.</summary>
    [DataField]
    public ProtoId<EmotePrototype> Emote;
}

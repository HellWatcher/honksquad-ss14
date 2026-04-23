using Content.Shared.Actions;

namespace Content.Shared.RussStation.VerbBindings;

/// <summary>
/// HONK Fires when the player triggers a <see cref="HonkEmoteActionComponent"/>-tagged action.
/// The server system reads the action entity's <c>HonkEmoteActionComponent.Emote</c> and plays it.
/// </summary>
public sealed partial class HonkEmoteActionEvent : InstantActionEvent
{
}

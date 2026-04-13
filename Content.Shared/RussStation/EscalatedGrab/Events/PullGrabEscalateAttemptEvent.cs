namespace Content.Shared.RussStation.EscalatedGrab.Events;

/// <summary>
/// Raised on the pulled entity when the puller tries to pull a target
/// they are already pulling. Subscribers can use this to escalate grabs.
/// If a subscriber handles the input it should set <see cref="Handled"/>;
/// otherwise the caller falls back to upstream toggle-off behaviour.
/// </summary>
[ByRefEvent]
public record struct PullGrabEscalateAttemptEvent(EntityUid Puller, EntityUid Pulled)
{
    public bool Handled;
}

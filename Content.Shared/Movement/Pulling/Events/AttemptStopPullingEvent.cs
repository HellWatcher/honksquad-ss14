namespace Content.Shared.Pulling.Events;

/// <summary>
/// Raised when a request is made to stop pulling an entity.
/// </summary>

[ByRefEvent]
//HONK START - Escalated grab: added Force parameter
public record struct AttemptStopPullingEvent(EntityUid? User = null, bool Force = false)
{
    public readonly EntityUid? User = User;
    public readonly bool Force = Force;
    //HONK END
    public bool Cancelled;
}

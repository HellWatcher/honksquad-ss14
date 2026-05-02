using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.EscalatedGrab;

/// <summary>
/// Escalation level of a grab. Re-clicking pull advances to the next stage.
/// </summary>
[Serializable, NetSerializable]
public enum GrabStage : byte
{
    /// <summary>
    /// Standard pull. Returned by GetStage when no escalation is active
    /// (no <see cref="Components.GrabStateComponent"/> on the puller).
    /// </summary>
    Pull = 0,

    /// <summary>
    /// Target locked to puller, cannot move independently. Can still act.
    /// </summary>
    Grab = 1,

    /// <summary>
    /// Target locked and cannot act (use/interact blocked). Enables easier stripping.
    /// </summary>
    Aggressive = 2,

    /// <summary>
    /// Target takes periodic stamina damage. Near-helpless.
    /// </summary>
    Choke = 3,
}

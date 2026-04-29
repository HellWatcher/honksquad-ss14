namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Marker for cleanbots that respond to nearby holders of the
/// <c>cleanbot_whisperer</c> capability tag. Server-side
/// CleanbotWhispererSystem polls these and emits a friendly greeting.
/// </summary>
[RegisterComponent]
public sealed partial class CleanbotWhispererBotComponent : Component
{
    [DataField]
    public float Range = CleanbotWhispererConstants.Range;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(CleanbotWhispererConstants.IntervalSeconds);

    [DataField]
    public TimeSpan NextGreeting;

    /// <summary>
    /// 1-in-N chance to roll the rare greeting instead of the common pool.
    /// </summary>
    [DataField]
    public int RareOneIn = CleanbotWhispererConstants.RareOneIn;
}

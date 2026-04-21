namespace Content.Shared.RussStation.Carrying;

public static class CarryingConstants
{
    public const float DefaultWalkSpeedModifier = 0.75f;

    public const float DefaultSprintSpeedModifier = 0.6f;

    public const float DefaultCarryOffset = 0.2f;

    public const int RequiredFreeHands = 2;

    public static readonly TimeSpan CarryDoAfterDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Horizontal component of the carried-entity visual offset. The offset lives
    /// entirely on the Y axis; X stays at the carrier's origin.
    /// </summary>
    public const float CarryOffsetX = 0f;

    /// <summary>
    /// How many hands a currently-pulling grip frees up when the pull is released
    /// as part of initiating a carry. Folded into the free-hand check so a carrier
    /// can lift someone they were already pulling without needing a third hand.
    /// </summary>
    public const int PullingFreesHands = 1;
}

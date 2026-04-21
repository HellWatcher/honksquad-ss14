namespace Content.Server.RussStation.Messenger;

public static class MessengerConstants
{
    /// <summary>
    /// Maximum attempts to generate a unique random address before falling back
    /// to a deterministic count-based address.
    /// </summary>
    public const int MaxAddressGenerationAttempts = 100;

    /// <summary>
    /// Upper bound (exclusive) for the 4-hex-digit random portion of a messenger
    /// address. Matches the <c>X4</c> hex format width.
    /// </summary>
    public const int CrewAddressHexRange = 0x10000;

    /// <summary>
    /// FIFO head index when trimming the oldest message off a conversation that
    /// has exceeded <see cref="MessengerServerSystem.MaxMessagesPerConversation"/>.
    /// </summary>
    public const int OldestMessageIndex = 0;

    /// <summary>
    /// Default "last-seen" message count used when a viewer has never opened a
    /// conversation with another cartridge.
    /// </summary>
    public const int NeverSeenMessageCount = 0;
}

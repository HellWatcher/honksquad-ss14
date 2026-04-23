using Content.Shared.Chat;

namespace Content.Client.UserInterface.Systems.Chat;

// Coalesce bookkeeping for a single (channel, sender, message) cluster. Lives in its own file so
// the HONK analyzer does not require a non-Honk counterpart declaration.
internal sealed class HonkChatCoalesceEntry
{
    public required ChatMessage Msg;
    public required string OriginalWrapped;
    public int Repeats;
    public TimeSpan LastSeen;
}

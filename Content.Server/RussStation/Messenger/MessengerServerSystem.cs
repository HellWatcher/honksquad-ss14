using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Content.Shared.RussStation.Messenger;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Messenger;

/// <summary>
/// Round-scoped message storage for the PDA messenger.
/// Messages are stored per-entity-pair and wiped on round restart.
/// </summary>
public sealed class MessengerServerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public const int MaxMessageLength = 256;
    public const int MaxMessagesPerConversation = 50;

    /// <summary>
    /// Messages keyed by a canonical pair of entity UIDs (lower UID first).
    /// </summary>
    private readonly Dictionary<(EntityUid, EntityUid), List<StoredMessage>> _messages = new();

    /// <summary>
    /// Tracks the message count each viewer last saw per conversation.
    /// </summary>
    private readonly Dictionary<(EntityUid Viewer, EntityUid Other), int> _lastSeen = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _messages.Clear();
        _lastSeen.Clear();
    }

    /// <summary>
    /// Store a message from sender to recipient. Returns false if the message is invalid.
    /// </summary>
    public bool SendMessage(EntityUid sender, EntityUid recipient, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Must have an ID inserted to send messages.
        if (!HasIdCard(sender))
            return false;

        if (text.Length > MaxMessageLength)
            text = text[..MaxMessageLength];

        var key = MakeKey(sender, recipient);
        if (!_messages.TryGetValue(key, out var conversation))
        {
            conversation = new List<StoredMessage>();
            _messages[key] = conversation;
        }

        conversation.Add(new StoredMessage(sender, text, _timing.CurTime));

        if (conversation.Count > MaxMessagesPerConversation)
            conversation.RemoveAt(0);

        return true;
    }

    /// <summary>
    /// Get all messages between two entities, formatted for a specific viewer.
    /// </summary>
    public List<MessengerMessageEntry> GetConversation(EntityUid viewer, EntityUid other)
    {
        var key = MakeKey(viewer, other);
        if (!_messages.TryGetValue(key, out var conversation))
            return new List<MessengerMessageEntry>();

        var entries = new List<MessengerMessageEntry>(conversation.Count);
        foreach (var msg in conversation)
        {
            var senderName = GetDisplayName(msg.Sender);
            entries.Add(new MessengerMessageEntry(senderName, msg.Text, msg.Timestamp, msg.Sender == viewer));
        }

        return entries;
    }

    /// <summary>
    /// Check if there are new messages since the viewer last opened this conversation.
    /// </summary>
    public bool HasUnread(EntityUid viewer, EntityUid other)
    {
        var key = MakeKey(viewer, other);
        if (!_messages.TryGetValue(key, out var conversation) || conversation.Count == 0)
            return false;

        var seenKey = (viewer, other);
        var lastSeen = _lastSeen.GetValueOrDefault(seenKey, 0);
        return conversation.Count > lastSeen;
    }

    /// <summary>
    /// Mark a conversation as read for a viewer.
    /// </summary>
    public void MarkRead(EntityUid viewer, EntityUid other)
    {
        var key = MakeKey(viewer, other);
        if (!_messages.TryGetValue(key, out var conversation))
            return;

        _lastSeen[(viewer, other)] = conversation.Count;
    }

    /// <summary>
    /// Build a contact list by scanning all PDAs that have a messenger cartridge installed.
    /// Uses the PDA's inserted ID card for name and job info, falls back to mob entity name.
    /// </summary>
    public List<MessengerContact> GetContacts(EntityUid mob)
    {
        var contacts = new List<MessengerContact>();
        var seen = new HashSet<EntityUid>();

        var query = EntityQueryEnumerator<MessengerCartridgeComponent>();
        while (query.MoveNext(out var cartUid, out _))
        {
            // Cartridge -> CartridgeLoader (PDA) -> mob
            var loaderUid = Transform(cartUid).ParentUid;
            if (!HasComp<CartridgeLoaderComponent>(loaderUid))
                continue;

            var target = Transform(loaderUid).ParentUid;
            if (target == mob || !seen.Add(target))
                continue;

            // No ID inserted means not visible as a contact.
            if (!TryComp<PdaComponent>(loaderUid, out var pda) || pda.ContainedId == null)
                continue;

            var pdaName = MetaData(loaderUid).EntityName;
            var excluded = IsExcludedPda(pdaName);

            // Excluded contacts only appear if they have a conversation with this mob.
            if (excluded && !HasConversation(mob, target))
                continue;

            var contact = BuildContact(mob, target, pda, excluded);
            contacts.Add(contact);
        }

        return contacts;
    }

    private const string UnknownName = "Unknown";

    private MessengerContact BuildContact(EntityUid mob, EntityUid target, PdaComponent pda, bool readOnly)
    {
        var name = UnknownName;
        var jobTitle = "";
        var jobIcon = "";

        if (pda.ContainedId is { } idUid &&
            TryComp<IdCardComponent>(idUid, out var idCard))
        {
            if (!string.IsNullOrEmpty(idCard.FullName))
                name = idCard.FullName;
            jobTitle = idCard.LocalizedJobTitle ?? "";
        }

        var netEntity = GetNetEntity(target);
        var unread = HasUnread(mob, target);
        return new MessengerContact(netEntity, name, jobTitle, jobIcon, unread, readOnly);
    }

    /// <summary>
    /// Check if a target entity has an excluded (antag) PDA, making them read-only.
    /// </summary>
    public bool IsContactReadOnly(EntityUid mob, EntityUid target)
    {
        var query = EntityQueryEnumerator<MessengerCartridgeComponent>();
        while (query.MoveNext(out var cartUid, out _))
        {
            var loaderUid = Transform(cartUid).ParentUid;
            if (!HasComp<CartridgeLoaderComponent>(loaderUid))
                continue;

            if (Transform(loaderUid).ParentUid != target)
                continue;

            return IsExcludedPda(MetaData(loaderUid).EntityName);
        }

        return false;
    }

    private bool HasConversation(EntityUid a, EntityUid b)
    {
        var key = MakeKey(a, b);
        return _messages.TryGetValue(key, out var msgs) && msgs.Count > 0;
    }

    private static readonly string[] ExcludedPdaKeywords =
    {
        "syndicate",
        "ninja",
        "pirate",
        "wizard",
        "CBURN",
    };

    private static bool IsExcludedPda(string pdaName)
    {
        foreach (var keyword in ExcludedPdaKeywords)
        {
            if (pdaName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool HasIdCard(EntityUid mob)
    {
        var query = EntityQueryEnumerator<PdaComponent, CartridgeLoaderComponent>();
        while (query.MoveNext(out var loaderUid, out var pda, out _))
        {
            if (Transform(loaderUid).ParentUid == mob)
                return pda.ContainedId != null;
        }

        return false;
    }

    private string GetDisplayName(EntityUid mob)
    {
        var query = EntityQueryEnumerator<PdaComponent, CartridgeLoaderComponent>();
        while (query.MoveNext(out var loaderUid, out var pda, out _))
        {
            if (Transform(loaderUid).ParentUid != mob)
                continue;

            if (pda.ContainedId is { } idUid &&
                TryComp<IdCardComponent>(idUid, out var idCard) &&
                !string.IsNullOrEmpty(idCard.FullName))
            {
                return idCard.FullName;
            }

            break;
        }

        return UnknownName;
    }

    private static (EntityUid, EntityUid) MakeKey(EntityUid a, EntityUid b)
    {
        return a.Id < b.Id ? (a, b) : (b, a);
    }

    private sealed class StoredMessage
    {
        public EntityUid Sender;
        public string Text;
        public TimeSpan Timestamp;

        public StoredMessage(EntityUid sender, string text, TimeSpan timestamp)
        {
            Sender = sender;
            Text = text;
            Timestamp = timestamp;
        }
    }
}

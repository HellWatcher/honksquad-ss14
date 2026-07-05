using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Content.Shared.RussStation.Messenger;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Messenger;

/// <summary>
/// Round-scoped message storage for the PDA messenger.
/// Messages are keyed by cartridge entity pairs and wiped on round restart.
/// Each cartridge gets a unique short address (like a MAC) on init.
/// </summary>
public sealed class MessengerServerSystem : EntitySystem, IMessengerMessageStore
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public const int MaxMessageLength = 256;
    public const int MaxMessagesPerConversation = 50;

    /// <summary>
    /// Default address prefix for station crew. Kept for backwards compatibility; the canonical
    /// value lives on <see cref="AntagAddressFilter"/>.
    /// </summary>
    public const string CrewAddressPrefix = AntagAddressFilter.CrewAddressPrefix;

    /// <summary>
    /// Messages keyed by canonical cartridge UID pair (lower first).
    /// </summary>
    private readonly Dictionary<(EntityUid, EntityUid), List<StoredMessage>> _messages = new();

    /// <summary>
    /// Tracks the message count each cartridge last saw per conversation.
    /// </summary>
    private readonly Dictionary<(EntityUid Viewer, EntityUid Other), int> _lastSeen = new();

    private readonly HashSet<string> _usedAddresses = new();

    private AntagAddressFilter _antagFilter = default!;
    private CartridgeIdentityValidator _identity = default!;
    private ContactListBuilder _contactBuilder = default!;

    public override void Initialize()
    {
        base.Initialize();

        _antagFilter = AntagAddressFilter.Default;
        _identity = new CartridgeIdentityValidator(EntityManager, _antagFilter);
        _contactBuilder = new ContactListBuilder(EntityManager, _identity, this);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<MessengerCartridgeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _messages.Clear();
        _lastSeen.Clear();
        _usedAddresses.Clear();
    }

    private void OnMapInit(EntityUid uid, MessengerCartridgeComponent comp, MapInitEvent args)
    {
        var loaderUid = Transform(uid).ParentUid;
        var prefix = _antagFilter.CrewPrefix;
        if (HasComp<PdaComponent>(loaderUid))
            prefix = _antagFilter.GetAddressPrefix(MetaData(loaderUid).EntityName);
        comp.Address = GenerateAddress(prefix);
        Dirty(uid, comp);
    }

    private string GenerateAddress(string prefix)
    {
        for (var i = 0; i < MessengerConstants.MaxAddressGenerationAttempts; i++)
        {
            var addr = $"{prefix}{_random.Next(MessengerConstants.CrewAddressHexRange):X4}";
            if (_usedAddresses.Add(addr))
                return addr;
        }

        // Random generation kept colliding; deterministically scan the address space for the first
        // free slot so we never silently hand out a duplicate address.
        for (var n = 0; n < MessengerConstants.CrewAddressHexRange; n++)
        {
            var addr = $"{prefix}{n:X4}";
            if (_usedAddresses.Add(addr))
                return addr;
        }

        // The entire 4-hex address space for this prefix is exhausted (~65k cartridges); a collision
        // is now unavoidable. This should never happen in a real round.
        Log.Error("Messenger address space exhausted for prefix '{Prefix}'; addresses may now collide.", prefix);
        return $"{prefix}{_random.Next(MessengerConstants.CrewAddressHexRange):X4}";
    }

    /// <summary>
    /// Store a message between two cartridges. Sender name is baked in from the ID card.
    /// </summary>
    public bool SendMessage(EntityUid senderCart, EntityUid recipientCart, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!TryComp<MessengerCartridgeComponent>(senderCart, out _))
            return false;

        // Must have an ID card to send.
        var senderName = _identity.GetCartridgeIdName(senderCart);
        if (senderName == null)
            return false;

        if (text.Length > MaxMessageLength)
            text = text[..MaxMessageLength];

        var key = MakeKey(senderCart, recipientCart);
        if (!_messages.TryGetValue(key, out var conversation))
        {
            conversation = new List<StoredMessage>();
            _messages[key] = conversation;
        }

        conversation.Add(new StoredMessage(senderCart, senderName, text, _timing.CurTime));

        if (conversation.Count > MaxMessagesPerConversation)
            conversation.RemoveAt(MessengerConstants.OldestMessageIndex);

        return true;
    }

    /// <summary>
    /// Get all messages between two cartridges, formatted for a specific viewer cartridge.
    /// </summary>
    public List<MessengerMessageEntry> GetConversation(EntityUid viewerCart, EntityUid otherCart)
    {
        var key = MakeKey(viewerCart, otherCart);
        if (!_messages.TryGetValue(key, out var conversation))
            return new List<MessengerMessageEntry>();

        var entries = new List<MessengerMessageEntry>(conversation.Count);
        foreach (var msg in conversation)
        {
            entries.Add(new MessengerMessageEntry(msg.SenderName, msg.Text, msg.Timestamp, msg.SenderCart == viewerCart));
        }

        return entries;
    }

    public bool HasUnread(EntityUid viewerCart, EntityUid otherCart)
    {
        var key = MakeKey(viewerCart, otherCart);
        if (!_messages.TryGetValue(key, out var conversation) || conversation.Count == 0)
            return false;

        var lastSeen = _lastSeen.GetValueOrDefault((viewerCart, otherCart), MessengerConstants.NeverSeenMessageCount);
        return conversation.Count > lastSeen;
    }

    public void MarkRead(EntityUid viewerCart, EntityUid otherCart)
    {
        var key = MakeKey(viewerCart, otherCart);
        if (!_messages.TryGetValue(key, out var conversation))
            return;

        _lastSeen[(viewerCart, otherCart)] = conversation.Count;
    }

    /// <summary>
    /// Build a contact list for a given cartridge. Scans all other cartridges.
    /// </summary>
    public List<MessengerContact> GetContacts(EntityUid myCart) => _contactBuilder.Build(myCart);

    /// <summary>
    /// Check if a target cartridge is read-only. A cartridge is writable only when it belongs
    /// to station crew: not an antag address, has an ID inserted, and that ID is on the station
    /// records roster (so CentComm and ERT IDs are excluded).
    /// </summary>
    public bool IsContactReadOnly(EntityUid targetCart) => !_identity.IsStationCrewCartridge(targetCart);

    /// <summary>
    /// Check if the cartridge's PDA has an ID card inserted.
    /// </summary>
    public bool HasIdCard(EntityUid cartUid) => _identity.HasIdCard(cartUid);

    /// <inheritdoc/>
    public bool HasConversation(EntityUid a, EntityUid b)
    {
        var key = MakeKey(a, b);
        return _messages.TryGetValue(key, out var msgs) && msgs.Count > 0;
    }

    /// <inheritdoc/>
    public IEnumerable<ConversationPartner> GetConversationPartners(EntityUid myCart)
    {
        foreach (var ((a, b), msgs) in _messages)
        {
            if (msgs.Count == 0)
                continue;

            var other = a == myCart ? b : b == myCart ? a : EntityUid.Invalid;
            if (other == EntityUid.Invalid)
                continue;

            // Use the last name the other party signed with, falling back to their address.
            var lastName = CompOrNull<MessengerCartridgeComponent>(other)?.Address ?? "?";
            for (var i = msgs.Count - 1; i >= 0; i--)
            {
                if (msgs[i].SenderCart == other)
                {
                    lastName = msgs[i].SenderName;
                    break;
                }
            }

            yield return new ConversationPartner(other, lastName);
        }
    }

    private static (EntityUid, EntityUid) MakeKey(EntityUid a, EntityUid b)
    {
        return a.Id < b.Id ? (a, b) : (b, a);
    }

    private sealed class StoredMessage
    {
        public EntityUid SenderCart;
        public string SenderName;
        public string Text;
        public TimeSpan Timestamp;

        public StoredMessage(EntityUid senderCart, string senderName, string text, TimeSpan timestamp)
        {
            SenderCart = senderCart;
            SenderName = senderName;
            Text = text;
            Timestamp = timestamp;
        }
    }
}

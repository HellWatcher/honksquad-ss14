using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
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
public sealed class MessengerServerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public const int MaxMessageLength = 256;
    public const int MaxMessagesPerConversation = 50;

    /// <summary>
    /// Address prefixes for antag categories. Any address starting with one of these is filtered.
    /// </summary>
    private static readonly Dictionary<string, string> AntagPrefixes = new()
    {
        { "syndicate", "SY" },
        { "ninja", "NJ" },
        { "pirate", "PR" },
        { "wizard", "WZ" },
        { "CBURN", "CB" },
    };

    /// <summary>
    /// Messages keyed by canonical cartridge UID pair (lower first).
    /// </summary>
    private readonly Dictionary<(EntityUid, EntityUid), List<StoredMessage>> _messages = new();

    /// <summary>
    /// Tracks the message count each cartridge last saw per conversation.
    /// </summary>
    private readonly Dictionary<(EntityUid Viewer, EntityUid Other), int> _lastSeen = new();

    private readonly HashSet<string> _usedAddresses = new();

    public override void Initialize()
    {
        base.Initialize();
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
        var prefix = CrewAddressPrefix;
        if (HasComp<PdaComponent>(loaderUid))
            prefix = GetAddressPrefix(MetaData(loaderUid).EntityName);
        comp.Address = GenerateAddress(prefix);
        Dirty(uid, comp);
    }

    public const string CrewAddressPrefix = "NT";

    private static string GetAddressPrefix(string pdaName)
    {
        foreach (var (keyword, prefix) in AntagPrefixes)
        {
            if (pdaName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return prefix;
        }

        return CrewAddressPrefix;
    }

    private string GenerateAddress(string prefix)
    {
        for (var i = 0; i < 100; i++)
        {
            var addr = $"{prefix}{_random.Next(0x10000):X4}";
            if (_usedAddresses.Add(addr))
                return addr;
        }

        var fallback = $"{prefix}{_usedAddresses.Count:X4}";
        _usedAddresses.Add(fallback);
        return fallback;
    }

    private static bool IsAntagAddress(string address)
    {
        foreach (var (_, prefix) in AntagPrefixes)
        {
            if (address.StartsWith(prefix))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Store a message between two cartridges. Sender name is baked in from the ID card.
    /// </summary>
    public bool SendMessage(EntityUid senderCart, EntityUid recipientCart, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!TryComp<MessengerCartridgeComponent>(senderCart, out var senderComp))
            return false;

        // Must have an ID card to send.
        var senderName = GetCartridgeIdName(senderCart);
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
            conversation.RemoveAt(0);

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

        var lastSeen = _lastSeen.GetValueOrDefault((viewerCart, otherCart), 0);
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
    public List<MessengerContact> GetContacts(EntityUid myCart)
    {
        if (!TryComp<MessengerCartridgeComponent>(myCart, out var myComp))
            return new List<MessengerContact>();

        var contacts = new List<MessengerContact>();
        var seen = new HashSet<EntityUid>();
        seen.Add(myCart);

        var query = EntityQueryEnumerator<MessengerCartridgeComponent>();
        while (query.MoveNext(out var cartUid, out var cartComp))
        {
            if (!seen.Add(cartUid))
                continue;

            var loaderUid = Transform(cartUid).ParentUid;
            if (!HasComp<CartridgeLoaderComponent>(loaderUid))
                continue;

            if (!TryComp<PdaComponent>(loaderUid, out var pda))
                continue;

            var isAntag = IsAntagAddress(cartComp.Address);
            var hasId = pda.ContainedId != null;

            // Antag cartridges: only show if there's a conversation.
            if (isAntag && !HasConversation(myCart, cartUid))
                continue;

            // No ID: only show if there's an existing conversation.
            if (!hasId && !HasConversation(myCart, cartUid))
                continue;

            var readOnly = isAntag || !hasId;
            var contact = BuildContact(myCart, cartUid, pda, readOnly);
            contacts.Add(contact);
        }

        // Find conversation partners whose cartridges aren't in the scan (destroyed, etc.)
        foreach (var ((a, b), msgs) in _messages)
        {
            if (msgs.Count == 0)
                continue;

            var other = a == myCart ? b : b == myCart ? a : EntityUid.Invalid;
            if (other == EntityUid.Invalid || !seen.Add(other))
                continue;

            if (!Exists(other))
                continue;

            // Use the last known sender name from the conversation.
            var lastName = CompOrNull<MessengerCartridgeComponent>(other)?.Address ?? "?";
            for (var i = msgs.Count - 1; i >= 0; i--)
            {
                if (msgs[i].SenderCart == other)
                {
                    lastName = msgs[i].SenderName;
                    break;
                }
            }

            contacts.Add(new MessengerContact(GetNetEntity(other), lastName, "", "", HasUnread(myCart, other), true));
        }

        return contacts;
    }

    private MessengerContact BuildContact(EntityUid myCart, EntityUid otherCart, PdaComponent pda, bool readOnly)
    {
        var name = CompOrNull<MessengerCartridgeComponent>(otherCart)?.Address ?? "?";
        var jobTitle = "";
        var jobIcon = "";

        if (pda.ContainedId is { } idUid &&
            TryComp<IdCardComponent>(idUid, out var idCard))
        {
            if (!string.IsNullOrEmpty(idCard.FullName))
                name = idCard.FullName;
            jobTitle = idCard.LocalizedJobTitle ?? "";
        }

        return new MessengerContact(GetNetEntity(otherCart), name, jobTitle, jobIcon, HasUnread(myCart, otherCart), readOnly);
    }

    /// <summary>
    /// Check if a target cartridge is read-only (antag or no ID).
    /// </summary>
    public bool IsContactReadOnly(EntityUid targetCart)
    {
        if (!TryComp<MessengerCartridgeComponent>(targetCart, out var comp))
            return true;

        if (IsAntagAddress(comp.Address))
            return true;

        var loaderUid = Transform(targetCart).ParentUid;
        if (!TryComp<PdaComponent>(loaderUid, out var pda) || pda.ContainedId == null)
            return true;

        return false;
    }

    /// <summary>
    /// Get the ID card name for a cartridge's PDA, or null if no ID is inserted.
    /// </summary>
    private string? GetCartridgeIdName(EntityUid cartUid)
    {
        var loaderUid = Transform(cartUid).ParentUid;
        if (!TryComp<PdaComponent>(loaderUid, out var pda) || pda.ContainedId == null)
            return null;

        if (TryComp<IdCardComponent>(pda.ContainedId.Value, out var idCard) &&
            !string.IsNullOrEmpty(idCard.FullName))
        {
            return idCard.FullName;
        }

        return CompOrNull<MessengerCartridgeComponent>(cartUid)?.Address ?? "?";
    }

    /// <summary>
    /// Check if the cartridge's PDA has an ID card inserted.
    /// </summary>
    public bool HasIdCard(EntityUid cartUid)
    {
        var loaderUid = Transform(cartUid).ParentUid;
        return TryComp<PdaComponent>(loaderUid, out var pda) && pda.ContainedId != null;
    }

    private bool HasConversation(EntityUid a, EntityUid b)
    {
        var key = MakeKey(a, b);
        return _messages.TryGetValue(key, out var msgs) && msgs.Count > 0;
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

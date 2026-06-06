using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Content.Shared.RussStation.Messenger;

namespace Content.Server.RussStation.Messenger;

/// <summary>
/// A conversation partner discovered from the message store, paired with the most recent name the
/// <em>other</em> party signed their messages with (used to label orphaned conversations).
/// </summary>
public readonly record struct ConversationPartner(EntityUid Other, string LastSenderName);

/// <summary>
/// Read-only view of the message store that <see cref="ContactListBuilder"/> needs. Keeping it
/// behind an interface lets the builder be exercised without a full message system.
/// </summary>
public interface IMessengerMessageStore
{
    /// <summary>True if the two cartridges have at least one stored message between them.</summary>
    bool HasConversation(EntityUid a, EntityUid b);

    /// <summary>True if <paramref name="viewerCart"/> has unread messages from <paramref name="otherCart"/>.</summary>
    bool HasUnread(EntityUid viewerCart, EntityUid otherCart);

    /// <summary>
    /// Every cartridge <paramref name="myCart"/> shares a non-empty conversation with, each paired
    /// with the most recent name the other party used.
    /// </summary>
    IEnumerable<ConversationPartner> GetConversationPartners(EntityUid myCart);
}

/// <summary>
/// Builds the messenger contact list for a cartridge in two named stages: discovering live
/// cartridges in the world, then reconciling orphaned conversations whose cartridges are no longer
/// in the scan (destroyed, unloaded, etc.).
/// </summary>
public sealed class ContactListBuilder
{
    private readonly IEntityManager _entMan;
    private readonly CartridgeIdentityValidator _identity;
    private readonly IMessengerMessageStore _store;

    public ContactListBuilder(
        IEntityManager entMan,
        CartridgeIdentityValidator identity,
        IMessengerMessageStore store)
    {
        _entMan = entMan;
        _identity = identity;
        _store = store;
    }

    /// <summary>
    /// Build the full contact list for <paramref name="myCart"/>.
    /// </summary>
    public List<MessengerContact> Build(EntityUid myCart)
    {
        if (!_entMan.HasComponent<MessengerCartridgeComponent>(myCart))
            return new List<MessengerContact>();

        var contacts = new List<MessengerContact>();
        var seen = new HashSet<EntityUid> { myCart };

        DiscoverLiveCartridges(myCart, contacts, seen);
        ReconcileOrphanedConversations(myCart, contacts, seen);

        return contacts;
    }

    /// <summary>
    /// Stage 1: scan every messenger cartridge in the world. Station crew always show up (writable);
    /// anyone else (antag, no ID, CentComm/ERT) only shows up when there's already a conversation,
    /// and is always read-only.
    /// </summary>
    private void DiscoverLiveCartridges(EntityUid myCart, List<MessengerContact> contacts, HashSet<EntityUid> seen)
    {
        var query = _entMan.EntityQueryEnumerator<MessengerCartridgeComponent>();
        while (query.MoveNext(out var cartUid, out var cartComp))
        {
            if (!seen.Add(cartUid))
                continue;

            if (!_identity.TryGetPda(cartUid, out var loaderUid, out var pda))
                continue;

            if (!_entMan.HasComponent<CartridgeLoaderComponent>(loaderUid))
                continue;

            var isCrew = _identity.IsStationCrew(cartComp.Address, pda);
            if (!isCrew && !_store.HasConversation(myCart, cartUid))
                continue;

            contacts.Add(BuildContact(myCart, cartUid, pda, readOnly: !isCrew));
        }
    }

    /// <summary>
    /// Stage 2: surface conversation partners whose cartridges weren't found in the scan (destroyed,
    /// unloaded, etc.). These are always read-only and labelled with the partner's last known name.
    /// </summary>
    private void ReconcileOrphanedConversations(EntityUid myCart, List<MessengerContact> contacts, HashSet<EntityUid> seen)
    {
        foreach (var partner in _store.GetConversationPartners(myCart))
        {
            if (!seen.Add(partner.Other))
                continue;

            if (!_entMan.EntityExists(partner.Other))
                continue;

            contacts.Add(new MessengerContact(
                _entMan.GetNetEntity(partner.Other),
                partner.LastSenderName,
                "",
                "",
                _store.HasUnread(myCart, partner.Other),
                true));
        }
    }

    /// <summary>
    /// Build a contact entry for a live cartridge, preferring the inserted ID's name and job title
    /// over the bare cartridge address.
    /// </summary>
    private MessengerContact BuildContact(EntityUid myCart, EntityUid otherCart, PdaComponent pda, bool readOnly)
    {
        var name = _entMan.GetComponentOrNull<MessengerCartridgeComponent>(otherCart)?.Address ?? "?";
        var jobTitle = "";
        var jobIcon = "";

        if (pda.ContainedId is { } idUid &&
            _entMan.TryGetComponent<IdCardComponent>(idUid, out var idCard))
        {
            if (!string.IsNullOrEmpty(idCard.FullName))
                name = idCard.FullName;
            jobTitle = idCard.LocalizedJobTitle ?? "";
        }

        return new MessengerContact(
            _entMan.GetNetEntity(otherCart),
            name,
            jobTitle,
            jobIcon,
            _store.HasUnread(myCart, otherCart),
            readOnly);
    }
}

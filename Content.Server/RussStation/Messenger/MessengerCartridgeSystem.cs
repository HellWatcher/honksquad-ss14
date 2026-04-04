using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Content.Shared.PDA.Ringer;
using Content.Shared.RussStation.Messenger;

namespace Content.Server.RussStation.Messenger;

public sealed class MessengerCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly MessengerServerSystem _messenger = default!;
    [Dependency] private readonly SharedRingerSystem _ringer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeActivatedEvent>(OnActivated);
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
    }

    private void OnUiReady(EntityUid uid, MessengerCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }

    private void OnActivated(EntityUid uid, MessengerCartridgeComponent component, CartridgeActivatedEvent args)
    {
        component.ActiveConversation = null;
        UpdateUiState(uid, args.Loader, component);
    }

    private void OnUiMessage(EntityUid uid, MessengerCartridgeComponent component, CartridgeMessageEvent args)
    {
        var loaderUid = GetEntity(args.LoaderUid);
        var mob = Transform(loaderUid).ParentUid;

        switch (args)
        {
            case MessengerRequestContactsMessage:
                component.ActiveConversation = null;
                break;

            case MessengerOpenConversationMessage open:
                component.ActiveConversation = GetEntity(open.Target);
                _messenger.MarkRead(mob, component.ActiveConversation.Value);
                break;

            case MessengerSendMessage send:
            {
                var target = GetEntity(send.Target);
                if (_messenger.IsContactReadOnly(mob, target))
                    break;
                if (_messenger.SendMessage(mob, target, send.Text))
                {
                    component.ActiveConversation = target;
                    _messenger.MarkRead(mob, target);
                    NotifyRecipient(target);
                }
                break;
            }

            case MessengerToggleMuteMessage:
                component.Muted = !component.Muted;
                Dirty(uid, component);
                break;
        }

        UpdateUiState(uid, loaderUid, component);
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, MessengerCartridgeComponent component)
    {
        var mob = Transform(loaderUid).ParentUid;
        var contacts = _messenger.GetContacts(mob);

        List<MessengerMessageEntry>? messages = null;
        NetEntity? activeNet = null;

        if (component.ActiveConversation != null && Exists(component.ActiveConversation.Value))
        {
            activeNet = GetNetEntity(component.ActiveConversation.Value);
            messages = _messenger.GetConversation(mob, component.ActiveConversation.Value);
        }

        var state = new MessengerUiState(contacts, activeNet, messages, component.Muted);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    /// <summary>
    /// Push updated state to the recipient and play their ringtone if not muted.
    /// </summary>
    private void NotifyRecipient(EntityUid recipient)
    {
        var query = EntityQueryEnumerator<CartridgeLoaderComponent>();
        while (query.MoveNext(out var loaderUid, out _))
        {
            if (Transform(loaderUid).ParentUid != recipient)
                continue;

            var cartridgeQuery = EntityQueryEnumerator<MessengerCartridgeComponent>();
            while (cartridgeQuery.MoveNext(out var cartUid, out var cartComp))
            {
                if (Transform(cartUid).ParentUid != loaderUid)
                    continue;

                UpdateUiState(cartUid, loaderUid, cartComp);

                if (!cartComp.Muted && TryComp<RingerComponent>(loaderUid, out var ringer))
                    _ringer.RingerPlayRingtone((loaderUid, ringer));

                return;
            }
        }
    }
}

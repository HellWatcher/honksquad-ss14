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
        Dirty(uid, component);
        UpdateUiState(uid, args.Loader, component);
    }

    private void OnUiMessage(EntityUid uid, MessengerCartridgeComponent component, CartridgeMessageEvent args)
    {
        var loaderUid = GetEntity(args.LoaderUid);

        switch (args)
        {
            case MessengerRequestContactsMessage:
                component.ActiveConversation = null;
                break;

            case MessengerOpenConversationMessage open:
                component.ActiveConversation = GetEntity(open.Target);
                _messenger.MarkRead(uid, component.ActiveConversation.Value);
                break;

            case MessengerSendMessage send:
            {
                var targetCart = GetEntity(send.Target);
                if (_messenger.IsContactReadOnly(targetCart))
                    break;
                if (_messenger.SendMessage(uid, targetCart, send.Text))
                {
                    component.ActiveConversation = targetCart;
                    _messenger.MarkRead(uid, targetCart);
                    NotifyRecipient(targetCart);
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
        var hasId = _messenger.HasIdCard(uid);
        var contacts = _messenger.GetContacts(uid);

        List<MessengerMessageEntry>? messages = null;
        NetEntity? activeNet = null;

        if (component.ActiveConversation != null && Exists(component.ActiveConversation.Value))
        {
            activeNet = GetNetEntity(component.ActiveConversation.Value);
            messages = _messenger.GetConversation(uid, component.ActiveConversation.Value);
        }

        var state = new MessengerUiState(contacts, activeNet, messages, component.Muted, hasId, component.Address);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    /// <summary>
    /// Push updated state to the recipient cartridge and play their PDA's ringtone if not muted.
    /// </summary>
    private void NotifyRecipient(EntityUid recipientCart)
    {
        if (!TryComp<MessengerCartridgeComponent>(recipientCart, out var cartComp))
            return;

        var loaderUid = Transform(recipientCart).ParentUid;
        if (!HasComp<CartridgeLoaderComponent>(loaderUid))
            return;

        UpdateUiState(recipientCart, loaderUid, cartComp);

        if (!cartComp.Muted && TryComp<RingerComponent>(loaderUid, out var ringer))
            _ringer.RingerPlayRingtone((loaderUid, ringer));
    }
}

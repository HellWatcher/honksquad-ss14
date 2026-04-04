using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.Access.Components;
using Content.Shared.PDA;
using Robust.Shared.Containers;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;

namespace Content.Server.RussStation.Economy;

public sealed class BalanceCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem? _cartridgeLoader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BalanceCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<BalanceCartridgeComponent, CartridgeActivatedEvent>(OnActivated);
        SubscribeLocalEvent<BalanceCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<PlayerBalanceComponent, BalanceChangedEvent>(OnBalanceChanged);
        SubscribeLocalEvent<IdCardComponent, EntGotInsertedIntoContainerMessage>(OnIdContainerChanged);
        SubscribeLocalEvent<IdCardComponent, EntGotRemovedFromContainerMessage>(OnIdContainerChanged);
    }

    private void OnIdContainerChanged(EntityUid uid, IdCardComponent comp, ContainerModifiedMessage args)
    {
        // The container owner is the PDA (if this ID was inserted/removed from one).
        if (TryComp<PdaComponent>(args.Container.Owner, out _))
            UpdateUiState(default, args.Container.Owner);
    }

    private void OnUiReady(EntityUid uid, BalanceCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader);
    }

    private void OnActivated(EntityUid uid, BalanceCartridgeComponent component, CartridgeActivatedEvent args)
    {
        UpdateUiState(uid, args.Loader);
    }

    private void OnUiMessage(EntityUid uid, BalanceCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not TogglePaycheckMuteMessage)
            return;

        var loaderUid = GetEntity(args.LoaderUid);
        if (!TryComp<PdaComponent>(loaderUid, out var pda) || pda.ContainedId == null)
            return;

        var holder = Transform(loaderUid).ParentUid;
        if (!TryComp<PlayerBalanceComponent>(holder, out var balance))
            return;

        balance.PaycheckMuted = !balance.PaycheckMuted;
        Dirty(holder, balance);
        UpdateUiState(uid, loaderUid);
    }

    private void OnBalanceChanged(EntityUid uid, PlayerBalanceComponent component, BalanceChangedEvent args)
    {
        // Find any PDA cartridge loaders held by this mob and push updated state.
        var query = EntityQueryEnumerator<CartridgeLoaderComponent>();
        while (query.MoveNext(out var loaderUid, out _))
        {
            if (Transform(loaderUid).ParentUid == uid)
                UpdateUiState(default, loaderUid);
        }
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid)
    {
        // Require an ID card to be inserted to show wallet data.
        if (!TryComp<PdaComponent>(loaderUid, out var pda) || pda.ContainedId == null)
        {
            var empty = new BalanceCartridgeUiState(0, string.Empty, false, new List<TransactionRecord>(), false);
            _cartridgeLoader?.UpdateCartridgeUiState(loaderUid, empty);
            return;
        }

        var holder = Transform(loaderUid).ParentUid;
        var comp = CompOrNull<PlayerBalanceComponent>(holder);
        var balance = comp?.Balance ?? 0;
        var accountNumber = comp?.AccountNumber ?? string.Empty;
        var suffix = accountNumber.Length >= 4
            ? accountNumber[^4..]
            : accountNumber;
        var muted = comp?.PaycheckMuted ?? false;
        var transactions = comp?.Transactions ?? new List<TransactionRecord>();

        var state = new BalanceCartridgeUiState(balance, suffix, muted, transactions, true);
        _cartridgeLoader?.UpdateCartridgeUiState(loaderUid, state);
    }
}

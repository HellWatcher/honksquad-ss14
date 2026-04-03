using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
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
        SubscribeLocalEvent<PlayerBalanceComponent, BalanceChangedEvent>(OnBalanceChanged);
    }

    private void OnUiReady(EntityUid uid, BalanceCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader);
    }

    private void OnActivated(EntityUid uid, BalanceCartridgeComponent component, CartridgeActivatedEvent args)
    {
        UpdateUiState(uid, args.Loader);
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
        // The loader (PDA) is held by the mob, which is its transform parent.
        var holder = Transform(loaderUid).ParentUid;
        var comp = CompOrNull<PlayerBalanceComponent>(holder);
        var balance = comp?.Balance ?? 0;
        var accountNumber = comp?.AccountNumber ?? string.Empty;
        var suffix = accountNumber.Length >= 4
            ? accountNumber[^4..]
            : accountNumber;

        var state = new BalanceCartridgeUiState(balance, suffix);
        _cartridgeLoader?.UpdateCartridgeUiState(loaderUid, state);
    }
}

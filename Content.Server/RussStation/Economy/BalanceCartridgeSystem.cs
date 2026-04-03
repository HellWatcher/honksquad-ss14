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
    }

    private void OnUiReady(EntityUid uid, BalanceCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader);
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid)
    {
        // The loader (PDA) is held by the mob, which is its transform parent.
        var holder = Transform(loaderUid).ParentUid;
        var balance = CompOrNull<PlayerBalanceComponent>(holder)?.Balance ?? 0;

        var state = new BalanceCartridgeUiState(balance);
        _cartridgeLoader?.UpdateCartridgeUiState(loaderUid, state);
    }
}

using System.Linq;
using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Content.Shared.RussStation.CartridgeLoader;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.CartridgeLoader;

/// <summary>
/// Installs fork-specific cartridges on all PDAs at map init,
/// driven by ForkCartridgeSetPrototype definitions rather than per-entity YAML.
/// Relies on <see cref="CartridgeLoaderSystem.InstallProgram"/> being idempotent
/// (HONK in upstream): duplicate calls for the same prototype no-op, so this
/// system doesn't need ordering or its own bookkeeping set.
/// </summary>
public sealed class PdaCartridgeInstallerSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PdaComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, PdaComponent pda, MapInitEvent args)
    {
        if (!TryComp<CartridgeLoaderComponent>(uid, out var loader))
            return;

        var sets = _protoManager.EnumeratePrototypes<ForkCartridgeSetPrototype>()
            .OrderBy(s => s.Order);

        foreach (var set in sets)
        {
            if (!MatchesFilter(uid, set))
                continue;

            foreach (var cartridge in set.Cartridges)
                _cartridgeLoader.InstallProgram(uid, cartridge, deinstallable: false, loader: loader);
        }
    }

    private bool MatchesFilter(EntityUid uid, ForkCartridgeSetPrototype set)
    {
        if (set.ExcludeComponents != null)
        {
            foreach (var compName in set.ExcludeComponents)
            {
                var reg = _compFactory.GetRegistration(compName);
                if (HasComp(uid, reg.Type))
                    return false;
            }
        }

        if (set.RequireComponents == null || set.RequireComponents.Count == 0)
            return true;

        foreach (var compName in set.RequireComponents)
        {
            var reg = _compFactory.GetRegistration(compName);
            if (!HasComp(uid, reg.Type))
                return false;
        }

        return true;
    }
}

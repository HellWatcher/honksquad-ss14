using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.RussStation.CartridgeLoader;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.CartridgeLoader;

/// <summary>
/// Installs fork-specific cartridges on all PDAs at map init,
/// driven by ForkCartridgeSetPrototype definitions rather than per-entity YAML.
/// </summary>
public sealed class PdaCartridgeInstallerSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CartridgeLoaderComponent, MapInitEvent>(OnMapInit,
            after: [typeof(CartridgeLoaderSystem)]);
    }

    private void OnMapInit(EntityUid uid, CartridgeLoaderComponent loader, MapInitEvent args)
    {
        var installed = _cartridgeLoader.GetInstalled(uid);
        var existing = new HashSet<string>();
        foreach (var prog in installed)
        {
            if (MetaData(prog).EntityPrototype?.ID is { } id)
                existing.Add(id);
        }

        var sets = _protoManager.EnumeratePrototypes<ForkCartridgeSetPrototype>()
            .OrderBy(s => s.Order);

        foreach (var set in sets)
        {
            if (!MatchesFilter(uid, set))
                continue;

            foreach (var cartridge in set.Cartridges)
            {
                if (existing.Contains(cartridge))
                    continue;

                _cartridgeLoader.InstallProgram(uid, cartridge, deinstallable: false, loader: loader);
                existing.Add(cartridge);
            }
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

using System.Linq;
using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Content.Shared.RussStation.CartridgeLoader;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.CartridgeLoader;

/// <summary>
/// Installs fork-specific cartridges on all PDAs at map init,
/// driven by ForkCartridgeSetPrototype definitions rather than per-entity YAML.
/// Installation is idempotent: a prototype that is already present on the loader
/// is skipped, so overlapping sets (and re-runs) never stack duplicates.
/// This guard used to live in the fork's copy of CartridgeLoaderSystem.InstallProgram;
/// upstream's shared InstallProgram does not dedupe (only InstallCartridge does),
/// so the check is kept here instead.
/// </summary>
public sealed partial class PdaCartridgeInstallerSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private IComponentFactory _compFactory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PdaComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, PdaComponent pda, MapInitEvent args)
    {
        if (!TryComp<CartridgeLoaderComponent>(uid, out var loader))
            return;

        var ent = new Entity<CartridgeLoaderComponent>(uid, loader);

        var sets = _protoManager.EnumeratePrototypes<ForkCartridgeSetPrototype>()
            .OrderBy(s => s.Order);

        foreach (var set in sets)
        {
            if (!MatchesFilter(uid, set))
                continue;

            foreach (var cartridge in set.Cartridges)
            {
                if (IsInstalled(uid, cartridge))
                    continue;

                _cartridgeLoader.InstallProgram(ent, cartridge, deinstallable: false);
            }
        }
    }

    /// <summary>
    /// Whether a program with the given prototype id is already on the loader,
    /// in either the removable or the preinstalled container.
    /// </summary>
    private bool IsInstalled(EntityUid uid, string prototype)
    {
        foreach (var program in _cartridgeLoader.GetDiskPrograms(uid))
        {
            if (MetaData(program).EntityPrototype?.ID == prototype)
                return true;
        }

        return false;
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

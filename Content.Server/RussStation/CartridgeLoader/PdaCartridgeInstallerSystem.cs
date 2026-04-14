using System.Linq;
using Content.Server.CartridgeLoader;
using Content.Shared.PDA;
using Content.Shared.RussStation.CartridgeLoader;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.CartridgeLoader;

/// <summary>
/// Contributes fork-specific cartridges to the initial install set on PDAs,
/// driven by ForkCartridgeSetPrototype definitions rather than per-entity YAML.
/// Hooks the upstream CartridgeLoaderInitialProgramsEvent seam, so cartridges
/// land in the same install pass as PreinstalledPrograms — no after-ordering,
/// no idempotency tracking against already-installed state.
/// </summary>
public sealed class PdaCartridgeInstallerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CartridgeLoaderInitialProgramsEvent>(OnInitialPrograms);
    }

    private void OnInitialPrograms(ref CartridgeLoaderInitialProgramsEvent args)
    {
        if (!HasComp<PdaComponent>(args.Loader))
            return;

        var sets = _protoManager.EnumeratePrototypes<ForkCartridgeSetPrototype>()
            .OrderBy(s => s.Order);

        foreach (var set in sets)
        {
            if (!MatchesFilter(args.Loader, set))
                continue;

            foreach (var cartridge in set.Cartridges)
                args.Programs.Add(cartridge);
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

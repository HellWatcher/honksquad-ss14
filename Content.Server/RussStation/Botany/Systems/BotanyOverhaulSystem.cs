using Content.Server.Botany.Components;
using Content.Server.RussStation.Botany.Components;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Robust.Shared.Configuration;

namespace Content.Server.RussStation.Botany.Systems;

/// <summary>
///     Proof-of-concept for gating a botany overhaul behind a CVar with zero edits to any
///     upstream botany system.
///
///     On map init, if <c>botany.overhaul_enabled</c> is set, every plant holder gets a
///     <see cref="BotanyOverhaulComponent"/> marker and the tweaks below take over. Flip the
///     CVar off (the default) and vanilla botany is completely untouched.
///
///     This deliberately reuses the stock <see cref="PlantHolderComponent"/> and only nudges
///     one field so the swap is observable and tiny. The full overhaul would instead replace
///     the component with an overhaul-owned one and run its own growth loop here.
/// </summary>
public sealed class BotanyOverhaulSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.BotanyOverhaulEnabled, value => _enabled = value, invokeImmediately: true);

        SubscribeLocalEvent<PlantHolderComponent, MapInitEvent>(OnPlantHolderMapInit);
        SubscribeLocalEvent<BotanyOverhaulComponent, ExaminedEvent>(OnOverhaulExamine);
    }

    private void OnPlantHolderMapInit(Entity<PlantHolderComponent> ent, ref MapInitEvent args)
    {
        if (!_enabled)
            return;

        // Mark the tray as overhauled so other overhaul code (and examine) can find it.
        EnsureComp<BotanyOverhaulComponent>(ent);

        // Observable proof the swap took effect: overhauled trays cycle growth twice as fast.
        ent.Comp.CycleDelay /= 2;
    }

    private void OnOverhaulExamine(Entity<BotanyOverhaulComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("botany-overhaul-poc-examine"));
    }
}

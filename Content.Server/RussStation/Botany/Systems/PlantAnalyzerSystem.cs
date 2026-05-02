using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Botany;
using Content.Shared.RussStation.Botany.Components;
using Content.Shared.Slippery;
using Content.Shared.Sprite;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Botany.Systems;

public sealed class PlantAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantAnalyzerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PlantAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<PlantAnalyzerComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<PlantAnalyzerComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<PlantAnalyzerComponent, DroppedEvent>(OnDropped);
    }

    private void OnMapInit(EntityUid uid, PlantAnalyzerComponent component, MapInitEvent args)
    {
        if (!_random.Prob(0.1f))
            return;

        var sprite = EnsureComp<RandomSpriteComponent>(uid);
        sprite.Selected["animation"] = ("analyzer-snake", null);
        Dirty(uid, sprite);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<PlantAnalyzerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var transform))
        {
            if (comp.NextUpdate > _timing.CurTime)
                continue;

            if (comp.ScannedEntity is not {} target)
                continue;

            if (Deleted(target))
            {
                StopAnalyzing((uid, comp), target);
                continue;
            }

            if (!TryGetSeed(target, out _))
            {
                StopAnalyzing((uid, comp), target);
                continue;
            }

            comp.NextUpdate = _timing.CurTime + comp.UpdateInterval;

            var targetCoords = Transform(target).Coordinates;
            if (comp.MaxScanRange != null && !_transformSystem.InRange(targetCoords, transform.Coordinates, comp.MaxScanRange.Value))
            {
                PauseAnalyzing((uid, comp), target);
                continue;
            }

            comp.IsAnalyzerActive = true;
            SendUiUpdate(uid, target);
        }
    }

    private void OnAfterInteract(Entity<PlantAnalyzerComponent> uid, ref AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach)
            return;

        if (!TryGetSeed(args.Target.Value, out _))
            return;

        _audio.PlayPvs(uid.Comp.ScanningBeginSound, uid);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, uid.Comp.ScanDelay,
            new PlantAnalyzerDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnDoAfter(Entity<PlantAnalyzerComponent> uid, ref PlantAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null)
            return;

        if (!TryGetSeed(args.Target.Value, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("plant-analyzer-no-plant"), args.User, args.User);
            return;
        }

        _audio.PlayPvs(uid.Comp.ScanningEndSound, uid);

        _uiSystem.OpenUi(uid.Owner, PlantAnalyzerUiKey.Key, args.User);
        BeginAnalyzing(uid, args.Target.Value);

        args.Handled = true;
    }

    private void OnInsertedIntoContainer(Entity<PlantAnalyzerComponent> uid, ref EntGotInsertedIntoContainerMessage args)
    {
        if (uid.Comp.ScannedEntity != null)
            _toggle.TryDeactivate(uid.Owner);
    }

    private void OnToggled(Entity<PlantAnalyzerComponent> uid, ref ItemToggledEvent args)
    {
        if (!args.Activated && uid.Comp.ScannedEntity is { } target)
            StopAnalyzing(uid, target);
    }

    private void OnDropped(Entity<PlantAnalyzerComponent> uid, ref DroppedEvent args)
    {
        if (uid.Comp.ScannedEntity != null)
            _toggle.TryDeactivate(uid.Owner);
    }

    private void BeginAnalyzing(Entity<PlantAnalyzerComponent> analyzer, EntityUid target)
    {
        analyzer.Comp.ScannedEntity = target;
        _toggle.TryActivate(analyzer.Owner);
        SendUiUpdate(analyzer, target);
    }

    private void StopAnalyzing(Entity<PlantAnalyzerComponent> analyzer, EntityUid target)
    {
        analyzer.Comp.ScannedEntity = null;
        _toggle.TryDeactivate(analyzer.Owner);
        SendUiUpdate(analyzer, target, scanMode: false);
    }

    private void PauseAnalyzing(Entity<PlantAnalyzerComponent> analyzer, EntityUid target)
    {
        if (!analyzer.Comp.IsAnalyzerActive)
            return;

        SendUiUpdate(analyzer, target, scanMode: false);
        analyzer.Comp.IsAnalyzerActive = false;
    }

    private void SendUiUpdate(EntityUid analyzer, EntityUid target, bool scanMode = true)
    {
        if (!_uiSystem.HasUi(analyzer, PlantAnalyzerUiKey.Key))
            return;

        if (!TryGetSeed(target, out var seed))
            return;

        var state = BuildState(target, seed!);
        state.ScanMode = scanMode;
        _uiSystem.SetUiState(analyzer, PlantAnalyzerUiKey.Key, new PlantAnalyzerScannedUserMessage(state));
    }

    private bool TryGetSeed(EntityUid target, out SeedData? seed)
    {
        if (TryComp<PlantHolderComponent>(target, out var holder) && holder.Seed != null)
        {
            seed = holder.Seed;
            return true;
        }

        if (TryComp<ProduceComponent>(target, out var produce) && _botany.TryGetSeed(produce, out seed))
            return true;

        seed = null;
        return false;
    }

    private PlantAnalyzerUiState BuildState(EntityUid target, SeedData seed)
    {
        var traits = new List<string>();

        if (seed.Seedless)
            traits.Add(Loc.GetString("plant-analyzer-trait-seedless"));
        if (!seed.Viable)
            traits.Add(Loc.GetString("plant-analyzer-trait-unviable"));
        if (seed.Ligneous)
            traits.Add(Loc.GetString("plant-analyzer-trait-ligneous"));
        if (seed.TurnIntoKudzu)
            traits.Add(Loc.GetString("plant-analyzer-trait-kudzufication"));
        if (seed.CanScream)
            traits.Add(Loc.GetString("plant-analyzer-trait-screaming"));
        if (HasComp<GhostTakeoverAvailableComponent>(target))
            traits.Add(Loc.GetString("plant-analyzer-trait-sentient"));
        if (HasComp<SlipperyComponent>(target))
            traits.Add(Loc.GetString("plant-analyzer-trait-slippery"));
        if (seed.SplatPrototype != null)
            traits.Add(Loc.GetString("plant-analyzer-trait-splatter"));

        var chemicals = new Dictionary<string, FixedPoint2>();
        foreach (var (reagentId, q) in seed.Chemicals)
        {
            var amount = q.Min;
            if (q.PotencyDivisor > 0 && seed.Potency > 0)
                amount += seed.Potency / q.PotencyDivisor;
            amount = FixedPoint2.Clamp(amount, q.Min, q.Max);

            if (!_prototypeManager.TryIndex<ReagentPrototype>(reagentId, out var reagent))
            {
                Log.Warning($"PlantAnalyzer: reagent '{reagentId}' not found for seed '{seed.Name}'.");
                continue;
            }

            chemicals[reagent.LocalizedName] = amount;
        }

        var consumeGases = BuildGasDict(seed.ConsumeGasses);
        var exudeGases = BuildGasDict(seed.ExudeGasses);

        string harvestRepeatKey;
        switch (seed.HarvestRepeat)
        {
            case HarvestType.NoRepeat:
                harvestRepeatKey = "plant-analyzer-harvest-no-repeat";
                break;
            case HarvestType.Repeat:
                harvestRepeatKey = "plant-analyzer-harvest-repeat";
                break;
            case HarvestType.SelfHarvest:
                harvestRepeatKey = "plant-analyzer-harvest-self-harvest";
                break;
            default:
                Log.Warning($"PlantAnalyzer: unrecognized HarvestType {seed.HarvestRepeat}.");
                harvestRepeatKey = "plant-analyzer-harvest-no-repeat";
                break;
        }

        return new PlantAnalyzerUiState
        {
            SeedName = Loc.GetString(seed.DisplayName),
            Lifespan = seed.Lifespan,
            Maturation = seed.Maturation,
            Production = seed.Production,
            Yield = seed.Yield,
            Potency = seed.Potency,
            GrowthStages = seed.GrowthStages,
            HarvestRepeat = Loc.GetString(harvestRepeatKey),
            Endurance = seed.Endurance,
            IdealLight = seed.IdealLight,
            WaterConsumption = seed.WaterConsumption,
            NutrientConsumption = seed.NutrientConsumption,
            IdealHeat = seed.IdealHeat,
            HeatTolerance = seed.HeatTolerance,
            LightTolerance = seed.LightTolerance,
            ToxinsTolerance = seed.ToxinsTolerance,
            LowPressureTolerance = seed.LowPressureTolerance,
            HighPressureTolerance = seed.HighPressureTolerance,
            PestTolerance = seed.PestTolerance,
            WeedTolerance = seed.WeedTolerance,
            Traits = traits,
            Chemicals = chemicals,
            ConsumeGases = consumeGases,
            ExudeGases = exudeGases,
        };
    }

    private Dictionary<string, float> BuildGasDict(Dictionary<Gas, float> gases)
    {
        var result = new Dictionary<string, float>();
        foreach (var (gas, rate) in gases)
        {
            var proto = _atmosphere.GetGas(gas);
            if (proto == null)
            {
                Log.Warning($"PlantAnalyzer: unknown gas ID {(int)gas}, skipping.");
                continue;
            }
            result[Loc.GetString(proto.Name)] = rate;
        }
        return result;
    }
}

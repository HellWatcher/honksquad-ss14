using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Botany;
using Content.Shared.RussStation.Botany.Components;
using Content.Shared.RussStation.Scanner;
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
        // Shared analyzer tick (rate limit, range-check, edge-paused on range exit); see ScannerUpdateHelper.
        var query = EntityQueryEnumerator<PlantAnalyzerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var transform))
        {
            var target = comp.ScannedEntity;
            var result = ScannerUpdateHelper.Evaluate(
                _transformSystem,
                _timing.CurTime,
                target,
                comp.NextUpdate,
                comp.UpdateInterval,
                comp.MaxScanRange,
                transform.Coordinates,
                isTargetGone: t => Deleted(t) || !TryGetSeed(t, out _),
                getTargetCoords: t => Transform(t).Coordinates);

            comp.NextUpdate = result.NextUpdate;

            switch (result.Action)
            {
                case ScannerUpdateHelper.ScanAction.Drop:
                    StopAnalyzing((uid, comp), target!.Value);
                    break;
                case ScannerUpdateHelper.ScanAction.Pause:
                    PauseAnalyzing((uid, comp), target!.Value);
                    break;
                case ScannerUpdateHelper.ScanAction.Push:
                    comp.IsAnalyzerActive = true;
                    SendUiUpdate(uid, target!.Value);
                    break;
            }
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
        return SeedDataFormatter.FormatSeedData(seed, target, EntityManager, _prototypeManager, _atmosphere, Log);
    }
}

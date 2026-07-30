using Content.Server.Medical.Components;
using Content.Shared.Body.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.MedicalScanner;
using Content.Shared.RussStation.MedicalScanner;
using Content.Shared.RussStation.Scanner;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using UpstreamHealthAnalyzerSystem = Content.Server.Medical.HealthAnalyzerSystem;

namespace Content.Server.RussStation.MedicalScanner;

/// <summary>
/// Reagent tab of the tabbed health analyzer UI. Drives the Reagents tab via
/// <see cref="HealthAnalyzerReagentScannerComponent"/>, which sits next to upstream's
/// <see cref="HealthAnalyzerComponent"/> so both systems can subscribe to the same
/// events without colliding in the (component, event) subscription slot.
///
/// Scans only fire for mobs: upstream's AfterInteract runs its DoAfter, and we piggyback
/// on <see cref="HealthAnalyzerDoAfterEvent"/> to push reagent state (bloodstream /
/// metabolites / stomachs / lungs) alongside the Health tab.
/// </summary>
public sealed partial class HealthAnalyzerReagentSystem : SharedHealthAnalyzerReagentSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private SolutionAggregator _aggregator = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Piggyback upstream's DoAfter so one scan populates both Health and Reagents tabs.
        SubscribeLocalEvent<HealthAnalyzerReagentScannerComponent, HealthAnalyzerDoAfterEvent>(OnHealthDoAfter,
            after: new[] { typeof(UpstreamHealthAnalyzerSystem) });

        SubscribeLocalEvent<HealthAnalyzerReagentScannerComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<HealthAnalyzerReagentScannerComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<HealthAnalyzerReagentScannerComponent, ItemToggledEvent>(OnToggled);
    }

    public override void Update(float frameTime)
    {
        // Shared analyzer tick (rate limit, range-check, edge-paused on range exit); see ScannerUpdateHelper.
        var query = EntityQueryEnumerator<HealthAnalyzerReagentScannerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var scanner, out var xform))
        {
            var target = scanner.ReagentScanTarget;
            var result = ScannerUpdateHelper.Evaluate(
                _transform,
                _timing.CurTime,
                target,
                scanner.NextReagentUpdate,
                scanner.ReagentUpdateInterval,
                scanner.MaxReagentScanRange,
                xform.Coordinates,
                isTargetGone: t => Deleted(t),
                getTargetCoords: t => Transform(t).Coordinates);

            scanner.NextReagentUpdate = result.NextUpdate;

            switch (result.Action)
            {
                case ScannerUpdateHelper.ScanAction.Drop:
                    StopReagentScan((uid, scanner));
                    break;
                case ScannerUpdateHelper.ScanAction.Pause:
                    PauseReagentScan((uid, scanner), target!.Value);
                    break;
                case ScannerUpdateHelper.ScanAction.Push:
                    scanner.IsReagentScanActive = true;
                    Dirty(uid, scanner);
                    PushReagentState(uid, target!.Value, active: true);
                    break;
            }
        }
    }

    private void OnHealthDoAfter(Entity<HealthAnalyzerReagentScannerComponent> ent, ref HealthAnalyzerDoAfterEvent args)
    {
        // Upstream sets Handled on success; skip only if Cancelled / missing target.
        if (args.Cancelled || args.Target is not { } target)
            return;

        if (!_ui.HasUi(ent.Owner, HealthAnalyzerUiKey.Key))
            return;

        // Drop any prior scan pin so switching targets doesn't keep streaming the old mob.
        StopReagentScan(ent);

        // Only track live reagent updates if the mob actually exposes reagents worth watching.
        // PreferredTab = Health so a fresh scan defaults back to the Health tab even if the
        // player had switched to Reagents on a prior scan.
        if (!HasComp<BloodstreamComponent>(target))
        {
            PushEmptyReagentState(ent.Owner, target, preferredTab: HealthAnalyzerTab.Health);
            return;
        }

        ent.Comp.ReagentScanTarget = target;
        ent.Comp.NextReagentUpdate = _timing.CurTime + ent.Comp.ReagentUpdateInterval;
        ent.Comp.IsReagentScanActive = true;
        Dirty(ent);
        PushReagentState(ent.Owner, target, active: true, preferredTab: HealthAnalyzerTab.Health);
    }

    private void PushEmptyReagentState(EntityUid analyzer, EntityUid target, HealthAnalyzerTab? preferredTab = null)
    {
        if (!_ui.HasUi(analyzer, HealthAnalyzerUiKey.Key))
            return;

        var displayName = Identity.Name(target, EntityManager);
        var empty = new HealthAnalyzerReagentState(GetNetEntity(target), displayName,
            new List<HealthAnalyzerReagentGroup>(), active: true, preferredTab: preferredTab);
        _ui.SetUiState(analyzer, HealthAnalyzerUiKey.Key, empty);
    }

    private void PushReagentState(EntityUid analyzer, EntityUid target, bool active, HealthAnalyzerTab? preferredTab = null)
    {
        if (!_ui.HasUi(analyzer, HealthAnalyzerUiKey.Key))
            return;

        var state = BuildState(target);
        state.Active = active;
        state.PreferredTab = preferredTab;
        _ui.SetUiState(analyzer, HealthAnalyzerUiKey.Key, state);
    }

    private void PauseReagentScan(Entity<HealthAnalyzerReagentScannerComponent> ent, EntityUid target)
    {
        if (!ent.Comp.IsReagentScanActive)
            return;

        ent.Comp.IsReagentScanActive = false;
        Dirty(ent);
        PushReagentState(ent.Owner, target, active: false);
    }

    private void StopReagentScan(Entity<HealthAnalyzerReagentScannerComponent> ent)
    {
        ent.Comp.ReagentScanTarget = null;
        ent.Comp.IsReagentScanActive = false;
        Dirty(ent);
    }

    private void OnDropped(Entity<HealthAnalyzerReagentScannerComponent> ent, ref DroppedEvent args)
    {
        StopReagentScan(ent);
        if (_ui.HasUi(ent.Owner, HealthAnalyzerUiKey.Key))
            _ui.CloseUi(ent.Owner, HealthAnalyzerUiKey.Key);
    }

    private void OnInsertedIntoContainer(Entity<HealthAnalyzerReagentScannerComponent> ent, ref EntGotInsertedIntoContainerMessage args)
        => StopReagentScan(ent);

    private void OnToggled(Entity<HealthAnalyzerReagentScannerComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated)
            StopReagentScan(ent);
    }

    /// <summary>
    /// Builds the full reagent UI state for <paramref name="target"/>. Group construction is
    /// delegated to <see cref="SolutionAggregator"/>; this wrapper just attaches the display name.
    /// </summary>
    public HealthAnalyzerReagentState BuildState(EntityUid target)
    {
        var groups = _aggregator.BuildGroups(target);
        var displayName = Identity.Name(target, EntityManager);
        return new HealthAnalyzerReagentState(GetNetEntity(target), displayName, groups);
    }
}

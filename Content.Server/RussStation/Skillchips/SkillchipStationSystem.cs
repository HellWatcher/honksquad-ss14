using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server.RussStation.Skillchips;

/// <summary>
/// Drives the skillchip station console: the chip-tray interaction and the
/// powered implant / remove operations. Occupant boarding lives in
/// <see cref="SkillchipStationOccupantManager"/> and UI-state serialization in
/// <see cref="SkillchipStationUiBuilder"/>; this system orchestrates them.
/// </summary>
public sealed class SkillchipStationSystem : EntitySystem
{
    [Dependency] private readonly SharedSkillchipSystem _skillchips = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SkillchipStationOccupantManager _occupant = default!;
    [Dependency] private readonly SkillchipStationUiBuilder _uiBuilder = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillchipStationComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SkillchipStationComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SkillchipStationComponent, SkillchipStationImplantMessage>(OnImplantMessage);
        SubscribeLocalEvent<SkillchipStationComponent, SkillchipStationEjectMessage>(OnEjectMessage);
        SubscribeLocalEvent<SkillchipStationComponent, SkillchipStationRemoveMessage>(OnRemoveMessage);
        SubscribeLocalEvent<SkillchipStationComponent, SkillchipImplantDoAfterEvent>(OnImplantDoAfter);
        SubscribeLocalEvent<SkillchipStationComponent, SkillchipRemoveDoAfterEvent>(OnRemoveDoAfter);
    }

    private void OnInit(EntityUid uid, SkillchipStationComponent comp, ComponentInit args)
    {
        _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.ChipSlotId);
    }

    // ── Chip-tray interaction ─────────────────────────────────────────────────

    private void OnInteractUsing(EntityUid uid, SkillchipStationComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<SkillchipComponent>(args.Used, out _))
            return;

        var slot = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.ChipSlotId);
        if (slot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-tray-full"), uid, args.User);
            args.Handled = true;
            return;
        }

        if (!_hands.TryDrop(args.User, args.Used, null, checkActionBlocker: false))
            return;

        _containers.Insert(args.Used, slot);
        args.Handled = true;
    }

    // ── Implant / eject / remove operations ───────────────────────────────────

    private void OnImplantMessage(EntityUid uid, SkillchipStationComponent comp, SkillchipStationImplantMessage args)
    {
        var user = args.Actor;

        if (!this.IsPowered(uid, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-no-power"), uid, user);
            return;
        }

        var slot = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.ChipSlotId);

        if (slot.ContainedEntity is not { } chipEnt)
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-no-chip"), uid, user);
            return;
        }

        if (!TryComp<SkillchipComponent>(chipEnt, out var chipComp))
            return;

        if (_occupant.TryGetOccupant(uid, out var patient) &&
            _skillchips.HasChipInstalled(patient, chipComp.ChipProto))
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-duplicate"), uid, user);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, user, comp.OperationDuration,
            new SkillchipImplantDoAfterEvent(), uid, target: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        TryStartOperation(uid, comp, doAfterArgs);
    }

    private void OnEjectMessage(EntityUid uid, SkillchipStationComponent comp, SkillchipStationEjectMessage args)
    {
        var user = args.Actor;
        var slot = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.ChipSlotId);

        if (slot.ContainedEntity is not { } chipEnt)
            return;

        _containers.Remove(chipEnt, slot);
        _hands.PickupOrDrop(user, chipEnt);
    }

    private void OnRemoveMessage(EntityUid uid, SkillchipStationComponent comp, SkillchipStationRemoveMessage args)
    {
        var user = args.Actor;

        if (!this.IsPowered(uid, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-no-power"), uid, user);
            return;
        }

        if (!_occupant.TryGetOccupant(uid, out var patient))
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-no-occupant"), uid, user);
            return;
        }

        if (!_skillchips.HasChipInstalled(patient, args.ChipProto))
            return;

        if (!_skillchips.TryGetBrain(patient, out _))
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-no-brain"), uid, user);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, user, comp.OperationDuration,
            new SkillchipRemoveDoAfterEvent(args.ChipProto), uid, target: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        TryStartOperation(uid, comp, doAfterArgs);
    }

    private void OnImplantDoAfter(EntityUid uid, SkillchipStationComponent comp, SkillchipImplantDoAfterEvent args)
    {
        var user = args.User;
        _uiBuilder.PushStateWorking(uid, comp, working: false);

        if (args.Cancelled)
            return;

        // Resolve the tray chip before the occupant: a chip pulled mid-operation
        // bails silently, exactly as the operator-facing flow always has.
        var slot = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.ChipSlotId);
        if (slot.ContainedEntity is not { } chipEnt || !TryComp<SkillchipComponent>(chipEnt, out var chipComp))
            return;

        if (!TryResolveOperationBrain(uid, user, popupOnMissing: true, out var brain))
            return;

        if (!_skillchips.TryInstall(brain, chipComp.ChipProto))
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-install-failed"), uid, user);
            return;
        }

        _containers.Remove(chipEnt, slot);
        QueueDel(chipEnt);
        _audio.PlayPvs(comp.OperationCompleteSound, uid);
        _popup.PopupEntity(Loc.GetString("skillchip-station-installed"), uid, user);
    }

    private void OnRemoveDoAfter(EntityUid uid, SkillchipStationComponent comp, SkillchipRemoveDoAfterEvent args)
    {
        var user = args.User;
        _uiBuilder.PushStateWorking(uid, comp, working: false);

        if (args.Cancelled)
            return;

        // The remove flow validated the occupant when it started the do-after,
        // so a target that has since left simply bails without a popup.
        if (!TryResolveOperationBrain(uid, user, popupOnMissing: false, out var brain))
            return;

        if (!_skillchips.TryRemove(brain, args.ChipProto))
            return;

        _audio.PlayPvs(comp.OperationCompleteSound, uid);
        _popup.PopupEntity(Loc.GetString("skillchip-station-removed"), uid, user);
    }

    // ── Shared do-after plumbing ──────────────────────────────────────────────

    /// <summary>
    /// Starts an operation do-after and, on success, plays the start cue and
    /// flips the UI into its working state. Shared by the implant and remove
    /// message handlers, which build identical do-afters bar their event.
    /// </summary>
    private void TryStartOperation(EntityUid uid, SkillchipStationComponent comp, DoAfterArgs doAfterArgs)
    {
        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        _audio.PlayPvs(comp.OperationStartSound, uid);
        _uiBuilder.PushStateWorking(uid, comp, working: true);
    }

    /// <summary>
    /// Resolves the seated patient's brain when an operation do-after completes.
    /// The implant flow surfaces the missing-occupant / missing-brain popups it
    /// has always shown via <paramref name="popupOnMissing"/>; the remove flow
    /// passes false because it validated the occupant before the do-after began.
    /// </summary>
    private bool TryResolveOperationBrain(EntityUid uid, EntityUid user, bool popupOnMissing, out Entity<SkillchipHolderComponent> brain)
    {
        brain = default;

        if (!_occupant.TryGetOccupant(uid, out var patient))
        {
            if (popupOnMissing)
                _popup.PopupEntity(Loc.GetString("skillchip-station-no-occupant"), uid, user);
            return false;
        }

        if (!_skillchips.TryGetBrain(patient, out brain))
        {
            if (popupOnMissing)
                _popup.PopupEntity(Loc.GetString("skillchip-station-no-brain"), uid, user);
            return false;
        }

        return true;
    }
}

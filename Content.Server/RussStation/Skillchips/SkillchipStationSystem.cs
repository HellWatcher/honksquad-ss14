using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Skillchips;

public sealed class SkillchipStationSystem : EntitySystem
{
    [Dependency] private readonly SharedSkillchipSystem _skillchips = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillchipStationComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SkillchipStationComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SkillchipStationComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<SkillchipStationComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<SkillchipStationComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
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

    private void OnUIOpened(EntityUid uid, SkillchipStationComponent comp, BoundUIOpenedEvent args)
    {
        PushState(uid, comp, args.Actor);
    }

    private void OnContainerChanged(EntityUid uid, SkillchipStationComponent comp, ContainerModifiedMessage args)
    {
        if (args.Container.ID != SkillchipStationComponent.ChipSlotId)
            return;

        foreach (var session in _ui.GetActors(uid, SkillchipStationUiKey.Key))
            PushState(uid, comp, session);
    }

    private void OnImplantMessage(EntityUid uid, SkillchipStationComponent comp, SkillchipStationImplantMessage args)
    {
        var user = args.Actor;
        var slot = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.ChipSlotId);

        if (slot.ContainedEntity is not { } chipEnt)
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-no-chip"), uid, user);
            return;
        }

        if (!TryComp<SkillchipComponent>(chipEnt, out _))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, comp.OperationDuration,
            new SkillchipImplantDoAfterEvent(), uid, target: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        _audio.PlayPvs(comp.OperationStartSound, uid);
        PushStateWorking(uid, comp, user, working: true);
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

        if (!_skillchips.HasChipInstalled(user, args.ChipProto))
            return;

        if (!_skillchips.TryGetBrain(user, out _))
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

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        _audio.PlayPvs(comp.OperationStartSound, uid);
        PushStateWorking(uid, comp, user, working: true);
    }

    /// <summary>
    /// The patient is whoever is strapped into the station, not the operator
    /// running the console.
    /// </summary>
    private bool TryGetOccupant(EntityUid uid, out EntityUid occupant)
    {
        occupant = default;
        if (!TryComp<StrapComponent>(uid, out var strap))
            return false;

        foreach (var buckled in strap.BuckledEntities)
        {
            occupant = buckled;
            return true;
        }

        return false;
    }

    private void OnImplantDoAfter(EntityUid uid, SkillchipStationComponent comp, SkillchipImplantDoAfterEvent args)
    {
        var user = args.User;
        PushStateWorking(uid, comp, user, working: false);

        if (args.Cancelled)
            return;

        var slot = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.ChipSlotId);
        if (slot.ContainedEntity is not { } chipEnt)
            return;

        if (!TryComp<SkillchipComponent>(chipEnt, out var chipComp))
            return;

        if (!TryGetOccupant(uid, out var patient))
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-no-occupant"), uid, user);
            return;
        }

        if (!_skillchips.TryGetBrain(patient, out var brain))
        {
            _popup.PopupEntity(Loc.GetString("skillchip-station-no-brain"), uid, user);
            return;
        }

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
        PushStateWorking(uid, comp, user, working: false);

        if (args.Cancelled)
            return;

        if (!TryGetOccupant(uid, out var patient) || !_skillchips.TryGetBrain(patient, out var brain))
            return;

        if (!_skillchips.TryRemove(brain, args.ChipProto))
            return;

        _audio.PlayPvs(comp.OperationCompleteSound, uid);
        _popup.PopupEntity(Loc.GetString("skillchip-station-removed"), uid, user);
    }

    // ── State helpers ─────────────────────────────────────────────────────────

    private void PushState(EntityUid uid, SkillchipStationComponent comp, EntityUid actor)
    {
        PushStateWorking(uid, comp, actor, false);
    }

    private void PushStateWorking(EntityUid uid, SkillchipStationComponent comp, EntityUid actor, bool working)
    {
        if (!_ui.IsUiOpen(uid, SkillchipStationUiKey.Key))
            return;

        var slot = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.ChipSlotId);
        SkillchipStationChipInfo? insertedInfo = null;

        if (slot.ContainedEntity is { } chipEnt && TryComp<SkillchipComponent>(chipEnt, out var chipComp))
            insertedInfo = BuildChipInfo(chipComp.ChipProto);

        var (usedCapacity, maxCapacity) = _skillchips.GetCapacity(actor);

        var implanted = new List<SkillchipStationChipInfo>();
        foreach (var protoId in _skillchips.GetInstalledChips(actor))
            implanted.Add(BuildChipInfo(protoId));

        var state = new SkillchipStationBoundUserInterfaceState(
            working, insertedInfo, implanted, usedCapacity, maxCapacity);

        _ui.SetUiState(uid, SkillchipStationUiKey.Key, state);
    }

    private SkillchipStationChipInfo BuildChipInfo(ProtoId<SkillchipPrototype> protoId)
    {
        var proto = _proto.Index(protoId);
        return new SkillchipStationChipInfo
        {
            ChipProto = protoId,
            Name = proto.Name,
            Description = proto.Description,
            CapacityCost = proto.CapacityCost,
        };
    }
}

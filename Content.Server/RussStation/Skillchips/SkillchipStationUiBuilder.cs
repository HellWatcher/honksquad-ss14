using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Skillchips;

/// <summary>
/// Serializes the station's bound-UI state: the inserted-tray chip, the seated
/// patient's installed chips, and their capacity bar. Split out of
/// <see cref="SkillchipStationSystem"/> so the refresh triggers (UI opened,
/// container changes) and the state-building both live in one place rather than
/// being interleaved with the implant/remove flow.
/// </summary>
public sealed class SkillchipStationUiBuilder : EntitySystem
{
    [Dependency] private readonly SkillchipStationOccupantManager _occupant = default!;
    [Dependency] private readonly SharedSkillchipSystem _skillchips = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillchipStationComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<SkillchipStationComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<SkillchipStationComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
    }

    private void OnUIOpened(EntityUid uid, SkillchipStationComponent comp, BoundUIOpenedEvent args)
    {
        PushState(uid, comp);
    }

    private void OnContainerChanged(EntityUid uid, SkillchipStationComponent comp, ContainerModifiedMessage args)
    {
        // Tray changes alter the inserted-chip panel; occupant changes alter
        // the installed-chip list and capacity bar. Both need a refresh.
        if (args.Container.ID != SkillchipStationComponent.ChipSlotId &&
            args.Container.ID != SkillchipStationComponent.BodyContainerId)
            return;

        PushState(uid, comp);
    }

    /// <summary>
    /// Pushes a fresh, non-working state to the open UI.
    /// </summary>
    public void PushState(EntityUid uid, SkillchipStationComponent comp)
    {
        PushStateWorking(uid, comp, false);
    }

    /// <summary>
    /// Pushes the UI state, optionally flagging an operation as in progress so
    /// the console shows its working indicator and disables further actions.
    /// </summary>
    public void PushStateWorking(EntityUid uid, SkillchipStationComponent comp, bool working)
    {
        if (!_ui.IsUiOpen(uid, SkillchipStationUiKey.Key))
            return;

        var slot = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.ChipSlotId);
        SkillchipStationChipInfo? insertedInfo = null;

        if (slot.ContainedEntity is { } chipEnt && TryComp<SkillchipComponent>(chipEnt, out var chipComp))
            insertedInfo = BuildChipInfo(chipComp.ChipProto);

        // Installed chips and capacity belong to the occupant being operated
        // on, not whoever opened the console. With no occupant the lists are
        // empty so the UI shows nothing to remove.
        var implanted = new List<SkillchipStationChipInfo>();
        var usedCapacity = 0;
        var maxCapacity = 0;

        if (_occupant.TryGetOccupant(uid, out var patient))
        {
            (usedCapacity, maxCapacity) = _skillchips.GetCapacity(patient);
            foreach (var protoId in _skillchips.GetInstalledChips(patient))
                implanted.Add(BuildChipInfo(protoId));
        }

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

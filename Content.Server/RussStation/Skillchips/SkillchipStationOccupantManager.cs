using Content.Shared.Body;
using Content.Shared.Climbing.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Movement.Events;
using Content.Shared.RussStation.Skillchips;
using Robust.Shared.Containers;

namespace Content.Server.RussStation.Skillchips;

/// <summary>
/// Owns the station's occupant slot: the body-container state machine that
/// boards a patient via drag-drop (medical-scanner / cryo-pod pattern), ejects
/// them on relay-move or destruction, and tracks the occupied/open appearance.
/// Split out of <see cref="SkillchipStationSystem"/> so the implant flow and the
/// UI builder can ask "who is seated?" without owning the boarding logic.
/// </summary>
public sealed partial class SkillchipStationOccupantManager : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillchipStationComponent, CanDropTargetEvent>(OnCanDragDropOn);
        SubscribeLocalEvent<SkillchipStationComponent, DragDropTargetEvent>(OnDragDropOn);
        SubscribeLocalEvent<SkillchipStationComponent, SkillchipStationEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<SkillchipStationComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);
        SubscribeLocalEvent<SkillchipStationComponent, DestructionEventArgs>(OnDestroyed);
    }

    /// <summary>
    /// Ensures the body container exists and seeds the occupied/open appearance.
    /// Driven from <see cref="SkillchipStationSystem"/>'s ComponentInit, which
    /// holds the component's single subscription to that event (Robust rejects a
    /// second subscriber for the same component/event pair).
    /// </summary>
    public void SetupOccupantContainer(EntityUid uid)
    {
        _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.BodyContainerId);
        UpdateAppearance(uid);
    }

    // ── Occupant insert / eject (medical scanner body-container pattern) ───────

    private void OnCanDragDropOn(EntityUid uid, SkillchipStationComponent comp, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop |= HasComp<BodyComponent>(args.Dragged) && !IsOccupied(uid);
    }

    private void OnDragDropOn(EntityUid uid, SkillchipStationComponent comp, ref DragDropTargetEvent args)
    {
        // Mirrors CryoPod.HandleDragDropOn: our own EntryDelay DoAfter, then
        // insert on completion. Mark handled so the climb system skips its
        // parallel DoAfter.
        args.Handled = true;

        if (IsOccupied(uid) || !HasComp<BodyComponent>(args.Dragged))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, comp.EntryDelay,
            new SkillchipStationEnterDoAfterEvent(), uid, target: args.Dragged, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnEnterDoAfter(EntityUid uid, SkillchipStationComponent comp, ref SkillchipStationEnterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } dragged)
            return;

        InsertBody(uid, dragged);
        args.Handled = true;
    }

    private void OnRelayMovement(EntityUid uid, SkillchipStationComponent comp, ref ContainerRelayMovementEntityEvent args)
    {
        EjectBody(uid);
    }

    private void OnDestroyed(EntityUid uid, SkillchipStationComponent comp, DestructionEventArgs args)
    {
        EjectBody(uid);
    }

    /// <summary>
    /// Returns true when a patient is seated in the body container.
    /// </summary>
    public bool IsOccupied(EntityUid uid)
    {
        var body = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.BodyContainerId);
        return body.ContainedEntity != null;
    }

    /// <summary>
    /// The patient is whoever is inside the body container, not the operator
    /// running the console.
    /// </summary>
    public bool TryGetOccupant(EntityUid uid, out EntityUid occupant)
    {
        occupant = default;
        var body = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.BodyContainerId);
        if (body.ContainedEntity is not { } contained)
            return false;

        occupant = contained;
        return true;
    }

    private void InsertBody(EntityUid uid, EntityUid toInsert)
    {
        if (!HasComp<BodyComponent>(toInsert) || IsOccupied(uid))
            return;

        var body = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.BodyContainerId);
        _containers.Insert(toInsert, body);
        UpdateAppearance(uid);
    }

    private void EjectBody(EntityUid uid)
    {
        var body = _containers.EnsureContainer<ContainerSlot>(uid, SkillchipStationComponent.BodyContainerId);
        if (body.ContainedEntity is not { } occupant)
            return;

        _containers.Remove(occupant, body);
        _climb.ForciblySetClimbing(occupant, uid);
        UpdateAppearance(uid);
    }

    private void UpdateAppearance(EntityUid uid)
    {
        var status = IsOccupied(uid) ? SkillchipStationStatus.Occupied : SkillchipStationStatus.Open;
        _appearance.SetData(uid, SkillchipStationVisuals.Status, status);
    }
}

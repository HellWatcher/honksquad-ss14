using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Carrying.Components;
using Content.Shared.RussStation.Carrying.Events;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.RussStation.EscalatedGrab;
using Content.Shared.RussStation.EscalatedGrab.Systems;
using Content.Shared.RussStation.Shared;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.Shared.RussStation.Carrying.Systems;

public abstract class SharedCarryingSystem : PairedMarkerSystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedEscalatedGrabSystem _grab = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    // Carriers currently setting up or tearing down a carry. While in this set,
    // OnVirtualItemDeleted won't call Drop(), preventing double-drop cascades.
    private readonly HashSet<EntityUid> _transitioning = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CarriableComponent, GetVerbsEvent<InteractionVerb>>(AddCarryVerb);
        SubscribeLocalEvent<BeingCarriedComponent, GetVerbsEvent<InteractionVerb>>(AddDropVerb);

        SubscribeLocalEvent<CarriableComponent, DragDropDraggedEvent>(OnDragDropDragged);
        SubscribeLocalEvent<CarriableComponent, CanDropDraggedEvent>(OnCanDropDragged);
        SubscribeLocalEvent<CarrierComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<CarrierComponent, CarryDoAfterEvent>(OnCarryDoAfter);
        SubscribeLocalEvent<ActiveCarrierComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<BeingCarriedComponent, UpdateCanMoveEvent>(OnCarriedCanMove);

        // Auto-drop conditions
        SubscribeLocalEvent<BeingCarriedComponent, MobStateChangedEvent>(OnCarriedMobStateChanged);
        SubscribeLocalEvent<BeingCarriedComponent, StoodEvent>(OnCarriedStood);
        SubscribeLocalEvent<BeingCarriedComponent, BuckledEvent>(OnCarriedBuckled);
        SubscribeLocalEvent<ActiveCarrierComponent, MobStateChangedEvent>(OnCarrierMobStateChanged);
        SubscribeLocalEvent<ActiveCarrierComponent, StunnedEvent>(OnCarrierStunned);
        SubscribeLocalEvent<ActiveCarrierComponent, DownedEvent>(OnCarrierDowned);
        SubscribeLocalEvent<BeingCarriedComponent, EntGotInsertedIntoContainerMessage>(OnCarriedInserted);
        SubscribeLocalEvent<ActiveCarrierComponent, EntGotInsertedIntoContainerMessage>(OnCarrierInserted);

        // Drop carry when the carried entity gets buckled
        SubscribeLocalEvent<BeingCarriedComponent, BuckleAttemptEvent>(OnCarriedBuckleAttempt);

        // Reactive escape detection: if something reparents the target away from the carrier
        // without going through Drop(), we need to tear the carry down immediately.
        SubscribeLocalEvent<BeingCarriedComponent, EntParentChangedMessage>(OnCarriedParentChanged);

        // Cleanup
        SubscribeLocalEvent<BeingCarriedComponent, ComponentShutdown>(OnBeingCarriedShutdown);
        SubscribeLocalEvent<ActiveCarrierComponent, ComponentShutdown>(OnActiveCarrierShutdown);
        SubscribeLocalEvent<ActiveCarrierComponent, DropHandItemsEvent>(OnDropHandItems);
        SubscribeLocalEvent<CarrierComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted);
    }

    #region Verbs

    private void AddCarryVerb(EntityUid uid, CarriableComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (args.User == args.Target)
            return;

        if (!CanCarry(args.User, args.Target))
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("carrying-verb-carry"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png")),
            Act = () =>
            {
                if (TryComp<CarrierComponent>(args.User, out var carrier) && CanCarry(args.User, args.Target))
                    StartCarryDoAfter(args.User, args.Target, carrier);
            },
        });
    }

    private void AddDropVerb(EntityUid uid, BeingCarriedComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (component.Carrier != args.User)
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("carrying-verb-drop"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/drop.svg.192dpi.png")),
            Act = () => Drop(args.User),
        });
    }

    #endregion

    #region Drag-Drop

    private void OnCanDropDragged(EntityUid uid, CarriableComponent component, ref CanDropDraggedEvent args)
    {
        if (args.Target != args.User)
            return;

        if (CanCarry(args.User, uid))
        {
            args.CanDrop = true;
            args.Handled = true;
        }
    }

    private void OnCanDropTarget(EntityUid uid, CarrierComponent component, ref CanDropTargetEvent args)
    {
        args.CanDrop = CanCarry(uid, args.Dragged);
        args.Handled = true;
    }

    private void OnDragDropDragged(EntityUid uid, CarriableComponent component, ref DragDropDraggedEvent args)
    {
        if (args.Handled || args.Target != args.User)
            return;

        if (!CanCarry(args.User, uid))
            return;

        if (!TryComp<CarrierComponent>(args.User, out var carrierComp))
            return;

        StartCarryDoAfter(args.User, uid, carrierComp);
        args.Handled = true;
    }

    #endregion

    #region Carry Logic

    private bool CanCarry(EntityUid carrier, EntityUid target)
    {
        if (carrier == target)
            return false;

        if (!HasComp<CarrierComponent>(carrier) || HasComp<ActiveCarrierComponent>(carrier))
            return false;

        if (!HasComp<CarriableComponent>(target) || HasComp<BeingCarriedComponent>(target))
            return false;

        if (_standing.IsDown(carrier) || _mobState.IsIncapacitated(carrier))
            return false;

        if (!_mobState.IsIncapacitated(target))
            return false;

        if (TryComp<BuckleComponent>(target, out var buckle) && buckle.Buckled)
            return false;

        if (!_actionBlocker.CanInteract(carrier, target))
            return false;

        // Requires an aggressive grab on the target.
        if (!_grab.HasStage(carrier, target, GrabStage.Aggressive))
            return false;

        // The pull's virtual item will be freed when the pull stops during Carry(),
        // so count it as available.
        var freeHands = _hands.CountFreeHands(carrier);
        var pullingTarget = TryComp<PullerComponent>(carrier, out var pullerCheck) && pullerCheck.Pulling == target;
        var effectiveFreeHands = freeHands + (pullingTarget ? CarryingConstants.PullingFreesHands : 0);
        if (effectiveFreeHands < CarryingConstants.RequiredFreeHands)
            return false;

        return true;
    }

    private void StartCarryDoAfter(EntityUid carrier, EntityUid target, CarrierComponent component)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, carrier, CarryingConstants.CarryDoAfterDuration, new CarryDoAfterEvent(), carrier, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnCarryDoAfter(EntityUid uid, CarrierComponent component, CarryDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null)
            return;

        args.Handled = true;
        Carry(uid, args.Target.Value);
    }

    /// <summary>
    /// Wires up the carry relationship: adds both marker components with their cross-references,
    /// reparents the target onto the carrier, spawns the virtual hand items, and locks the target's
    /// movement. Public so integration tests can set up a carry without satisfying the verb path's
    /// preconditions (aggressive grab, hand count, etc.).
    /// </summary>
    public void Carry(EntityUid carrier, EntityUid target)
    {
        if (HasComp<ActiveCarrierComponent>(carrier) || HasComp<BeingCarriedComponent>(target))
            return;

        if (!_standing.IsDown(target) && !_mobState.IsIncapacitated(target))
            return;

        var attempt = new CarryAttemptEvent(carrier, target);
        RaiseLocalEvent(carrier, ref attempt);
        if (attempt.Cancelled)
            return;
        RaiseLocalEvent(target, ref attempt);
        if (attempt.Cancelled)
            return;

        // Mark as transitioning for the full setup. The reparent below can trigger
        // other systems (like buckle) which may delete virtual items. Without this
        // guard, those deletions would call Drop() while we're still setting up.
        _transitioning.Add(carrier);

        if (TryComp<PullableComponent>(target, out var pullable) && pullable.Puller != null)
            _pulling.TryStopPull(target, pullable);

        if (TryComp<PullerComponent>(carrier, out var puller) && puller.Pulling != null
            && TryComp<PullableComponent>(puller.Pulling.Value, out var pullerPullable))
            _pulling.TryStopPull(puller.Pulling.Value, pullerPullable);

        var active = EnsureComp<ActiveCarrierComponent>(carrier);
        active.Target = target;
        Dirty(carrier, active);

        var being = EnsureComp<BeingCarriedComponent>(target);
        being.Carrier = carrier;
        Dirty(target, being);

        if (!_virtualItem.TrySpawnVirtualItemInHand(target, carrier))
            Log.Warning($"Failed to spawn first carry virtual item on {ToPrettyString(carrier)}");
        if (!_virtualItem.TrySpawnVirtualItemInHand(target, carrier))
            Log.Warning($"Failed to spawn second carry virtual item on {ToPrettyString(carrier)}");

        // Parent target to carrier. The client's FrameUpdate handles the visual offset.
        var xform = Transform(target);
        var coords = new EntityCoordinates(carrier, System.Numerics.Vector2.Zero);
        _transform.SetCoordinates(target, xform, coords, rotation: Angle.Zero);

        _transitioning.Remove(carrier);

        // The reparent above can cause other systems to move the target back (e.g.
        // buckle detecting a parent change and unbuckling). Reactive parent-change
        // handler will have called Drop() in that case; verify and bail if so.
        if (!HasComp<ActiveCarrierComponent>(carrier) || Transform(target).ParentUid != carrier)
            return;

        _joints.SetRelay(target, carrier);

        _standing.Down(target, playSound: false, dropHeldItems: false, force: true);

        if (TryComp<PhysicsComponent>(target, out var physics))
            _physics.ResetDynamics(target, physics);

        _movementSpeed.RefreshMovementSpeedModifiers(carrier);
        _actionBlocker.UpdateCanMove(target);

        _popup.PopupClient(Loc.GetString("carrying-start-carrier", ("target", target)), carrier, carrier);
        _popup.PopupClient(Loc.GetString("carrying-start-carried", ("carrier", carrier)), target, target);

        var ev = new CarryStartedEvent(carrier, target);
        RaiseLocalEvent(carrier, ref ev);
        RaiseLocalEvent(target, ref ev);
    }

    /// <summary>
    /// Public API to drop whoever <paramref name="carrier"/> is currently carrying, if any.
    /// </summary>
    public void Drop(EntityUid carrier)
    {
        if (!TryComp<ActiveCarrierComponent>(carrier, out var active))
            return;

        var target = active.Target;

        // Tearing down the relationship: removing the marker fires its shutdown handler,
        // which is responsible for removing the symmetric BeingCarriedComponent and
        // performing the visible cleanup (virtual items, reparent, joints, popups, events).
        // Marker removal is the single point of truth for ending a carry.
        RemComp<ActiveCarrierComponent>(carrier);
        DebugTools.Assert(Terminating(target) || !HasComp<BeingCarriedComponent>(target),
            "OnActiveCarrierShutdown should have removed the BeingCarriedComponent");
    }

    #endregion

    #region Speed & Movement

    private void OnRefreshMoveSpeed(EntityUid uid, ActiveCarrierComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<CarrierComponent>(uid, out var carrier))
            return;

        args.ModifySpeed(carrier.WalkSpeedModifier, carrier.SprintSpeedModifier);
    }

    private void OnCarriedCanMove(EntityUid uid, BeingCarriedComponent component, UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    #endregion

    #region Auto-Drop

    private void OnCarriedMobStateChanged(EntityUid uid, BeingCarriedComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            Drop(component.Carrier);
    }

    private void OnCarriedStood(EntityUid uid, BeingCarriedComponent component, StoodEvent args)
    {
        Drop(component.Carrier);
    }

    private void OnCarriedBuckled(EntityUid uid, BeingCarriedComponent component, ref BuckledEvent args)
    {
        Drop(component.Carrier);
    }

    private void OnCarrierMobStateChanged(EntityUid uid, ActiveCarrierComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            Drop(uid);
    }

    private void OnCarrierStunned(EntityUid uid, ActiveCarrierComponent component, ref StunnedEvent args)
    {
        Drop(uid);
    }

    private void OnCarrierDowned(EntityUid uid, ActiveCarrierComponent component, ref DownedEvent args)
    {
        Drop(uid);
    }

    private void OnCarriedInserted(EntityUid uid, BeingCarriedComponent component, EntGotInsertedIntoContainerMessage args)
    {
        Drop(component.Carrier);
    }

    private void OnCarrierInserted(EntityUid uid, ActiveCarrierComponent component, EntGotInsertedIntoContainerMessage args)
    {
        Drop(uid);
    }

    private void OnCarriedBuckleAttempt(EntityUid uid, BeingCarriedComponent component, ref BuckleAttemptEvent args)
    {
        Drop(component.Carrier);
    }

    private void OnCarriedParentChanged(EntityUid uid, BeingCarriedComponent component, ref EntParentChangedMessage args)
    {
        // Ignore the reparent we trigger ourselves during Carry() setup.
        if (_transitioning.Contains(component.Carrier))
            return;

        // Entity deletion detaches before component shutdown. Skip — the marker's
        // own ComponentShutdown handler will run the teardown for the deletion case.
        if (Terminating(uid))
            return;

        // PlaceNextTo inside OnBeingCarriedShutdown fires a parent change mid-teardown;
        // bail so we don't re-enter Drop on an already-Stopping component.
        if (IsShuttingDown(component))
            return;

        if (Transform(uid).ParentUid != component.Carrier)
            Drop(component.Carrier);
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// The single teardown path for a carry. Fires when the target's marker is removed —
    /// whether that removal came from <see cref="Drop"/>, the carrier-side shutdown handler,
    /// or because the target entity itself is being deleted. Reads the carrier reference
    /// off the marker so it never depends on any other component still being intact.
    /// </summary>
    private void OnBeingCarriedShutdown(EntityUid uid, BeingCarriedComponent component, ComponentShutdown args)
    {
        var carrier = component.Carrier;

        // Remove the symmetric carrier-side marker. TryRemovePaired skips if the carrier
        // is terminating or its marker is already shutting down — without that guard the
        // two handlers recurse (HasComp stays true during ComponentShutdown).
        TryRemovePaired<ActiveCarrierComponent>(carrier);

        if (Exists(carrier) && !Terminating(carrier))
        {
            _transitioning.Add(carrier);
            _virtualItem.DeleteInHandsMatching(carrier, uid);
            _transitioning.Remove(carrier);
            _movementSpeed.RefreshMovementSpeedModifiers(carrier);

            _popup.PopupClient(Loc.GetString("carrying-drop-carrier", ("target", uid)), carrier, carrier);
        }

        if (!Terminating(uid))
        {
            if (Exists(carrier) && !Terminating(carrier))
            {
                var targetXform = Transform(uid);
                if (targetXform.ParentUid == carrier)
                {
                    _transform.PlaceNextTo((uid, targetXform), (carrier, Transform(carrier)));
                    targetXform.ActivelyLerping = false;
                    Dirty(uid, targetXform);
                }
            }

            _joints.RefreshRelay(uid);
            _actionBlocker.UpdateCanMove(uid);

            if (!_mobState.IsIncapacitated(uid) && !HasComp<KnockedDownComponent>(uid))
                _standing.Stand(uid);

            _popup.PopupClient(Loc.GetString("carrying-drop-carried", ("carrier", carrier)), uid, uid);
        }

        var ev = new CarryStoppedEvent(carrier, uid);
        if (Exists(carrier))
            RaiseLocalEvent(carrier, ref ev);
        RaiseLocalEvent(uid, ref ev);
    }

    /// <summary>
    /// Symmetric handler for when the carrier-side marker is removed first — typically
    /// from <see cref="Drop"/> or the carrier entity itself being deleted. Mirrors removal
    /// to the target marker; the rest of the teardown then runs from
    /// <see cref="OnBeingCarriedShutdown"/>.
    /// </summary>
    private void OnActiveCarrierShutdown(EntityUid uid, ActiveCarrierComponent component, ComponentShutdown args)
    {
        // TryRemovePaired skips if the target is terminating or its marker is already
        // shutting down — without that guard the two handlers recurse when teardown
        // enters via BeingCarried first.
        TryRemovePaired<BeingCarriedComponent>(component.Target);
    }

    private void OnDropHandItems(EntityUid uid, ActiveCarrierComponent component, DropHandItemsEvent args)
    {
        Drop(uid);
    }

    private void OnVirtualItemDeleted(EntityUid uid, CarrierComponent component, VirtualItemDeletedEvent args)
    {
        if (_transitioning.Contains(uid))
            return;

        if (TryComp<ActiveCarrierComponent>(uid, out var active) && active.Target == args.BlockingEntity)
            Drop(uid);
    }

    #endregion
}

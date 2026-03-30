using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Carrying.Components;
using Content.Shared.RussStation.Carrying.Events;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.RussStation.EscalatedGrab;
using Content.Shared.RussStation.EscalatedGrab.Systems;
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
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.RussStation.Carrying.Systems;

public abstract class SharedCarryingSystem : EntitySystem
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
    [Dependency] private readonly IGameTiming _timing = default!;

    // Carriers currently setting up or tearing down a carry. While in this set,
    // OnVirtualItemDeleted won't call Drop(), preventing double-drop cascades.
    private readonly HashSet<EntityUid> _transitioning = new();

    private TimeSpan _nextValidation;
    private static readonly TimeSpan ValidationInterval = TimeSpan.FromSeconds(1);

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
        if (!TryComp<CarriableComponent>(uid, out var carriable) || carriable.CarriedBy != args.User)
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

        if (!TryComp<CarrierComponent>(carrier, out var carrierComp) || carrierComp.Carrying != null)
            return false;

        if (!TryComp<CarriableComponent>(target, out var carriableComp) || carriableComp.CarriedBy != null)
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
        var effectiveFreeHands = freeHands + (pullingTarget ? 1 : 0);
        if (effectiveFreeHands < 2)
            return false;

        return true;
    }

    private void StartCarryDoAfter(EntityUid carrier, EntityUid target, CarrierComponent component)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, carrier, TimeSpan.FromSeconds(3), new CarryDoAfterEvent(), carrier, target: target)
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
        Carry(uid, args.Target.Value, component);
    }

    private void Carry(EntityUid carrier, EntityUid target, CarrierComponent? carrierComp = null, CarriableComponent? carriableComp = null)
    {
        if (!Resolve(carrier, ref carrierComp) || !Resolve(target, ref carriableComp))
            return;

        if (carrierComp.Carrying != null || carriableComp.CarriedBy != null)
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

        carrierComp.Carrying = target;
        carriableComp.CarriedBy = carrier;
        Dirty(carrier, carrierComp);
        Dirty(target, carriableComp);

        EnsureComp<ActiveCarrierComponent>(carrier);
        EnsureComp<BeingCarriedComponent>(target);

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
        // buckle detecting a parent change and unbuckling). If that happened, clean
        // up instead of continuing with a half-built carry.
        if (carrierComp.Carrying != target || Transform(target).ParentUid != carrier)
        {
            Log.Warning($"Carry disrupted during setup: {ToPrettyString(carrier)} -> {ToPrettyString(target)}");
            CleanOrphanedCarryState(carrier, carrierComp);
            return;
        }

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

    public void Drop(EntityUid carrier, CarrierComponent? carrierComp = null)
    {
        if (!Resolve(carrier, ref carrierComp) || carrierComp.Carrying is not { } target)
            return;

        if (!TryComp<CarriableComponent>(target, out var carriableComp))
            return;

        carrierComp.Carrying = null;
        carriableComp.CarriedBy = null;
        Dirty(carrier, carrierComp);
        Dirty(target, carriableComp);

        // Remove immediately so carried-entity event handlers (like OnCarriedCanMove
        // and OnCarriedStood) don't fire after the drop.
        RemComp<BeingCarriedComponent>(target);
        RemComp<ActiveCarrierComponent>(carrier);

        _virtualItem.DeleteInHandsMatching(carrier, target);

        if (!Terminating(carrier) && !Terminating(target))
        {
            var targetXform = Transform(target);
            if (targetXform.ParentUid == carrier)
            {
                _transform.PlaceNextTo((target, targetXform), (carrier, Transform(carrier)));
                targetXform.ActivelyLerping = false;
            }
        }

        _joints.RefreshRelay(target);
        _movementSpeed.RefreshMovementSpeedModifiers(carrier);
        _actionBlocker.UpdateCanMove(target);

        if (!_mobState.IsIncapacitated(target) && !HasComp<KnockedDownComponent>(target))
            _standing.Stand(target);

        _popup.PopupClient(Loc.GetString("carrying-drop-carrier", ("target", target)), carrier, carrier);
        _popup.PopupClient(Loc.GetString("carrying-drop-carried", ("carrier", carrier)), target, target);

        var ev = new CarryStoppedEvent(carrier, target);
        RaiseLocalEvent(carrier, ref ev);
        RaiseLocalEvent(target, ref ev);
    }

    #endregion

    #region Speed & Movement

    private void OnRefreshMoveSpeed(EntityUid uid, ActiveCarrierComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<CarrierComponent>(uid, out var carrier) || carrier.Carrying == null)
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
        {
            if (TryComp<CarriableComponent>(uid, out var carriable) && carriable.CarriedBy is { } carrier)
                Drop(carrier);
        }
    }

    private void OnCarriedStood(EntityUid uid, BeingCarriedComponent component, StoodEvent args)
    {
        if (TryComp<CarriableComponent>(uid, out var carriable) && carriable.CarriedBy is { } carrier)
            Drop(carrier);
    }

    private void OnCarriedBuckled(EntityUid uid, BeingCarriedComponent component, ref BuckledEvent args)
    {
        if (TryComp<CarriableComponent>(uid, out var carriable) && carriable.CarriedBy is { } carrier)
            Drop(carrier);
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
        if (TryComp<CarriableComponent>(uid, out var carriable) && carriable.CarriedBy is { } carrier)
            Drop(carrier);
    }

    private void OnCarrierInserted(EntityUid uid, ActiveCarrierComponent component, EntGotInsertedIntoContainerMessage args)
    {
        Drop(uid);
    }

    #endregion

    #region Cleanup

    private void OnBeingCarriedShutdown(EntityUid uid, BeingCarriedComponent component, ComponentShutdown args)
    {
        if (TryComp<CarriableComponent>(uid, out var carriable) && carriable.CarriedBy is { } carrier && TryComp<CarrierComponent>(carrier, out var carrierComp))
        {
            carrierComp.Carrying = null;
            Dirty(carrier, carrierComp);
            RemCompDeferred<ActiveCarrierComponent>(carrier);

            _transitioning.Add(carrier);
            _virtualItem.DeleteInHandsMatching(carrier, uid);
            _transitioning.Remove(carrier);

            _movementSpeed.RefreshMovementSpeedModifiers(carrier);
        }
    }

    private void OnActiveCarrierShutdown(EntityUid uid, ActiveCarrierComponent component, ComponentShutdown args)
    {
        if (TryComp<CarrierComponent>(uid, out var carrier) && carrier.Carrying is { } target && TryComp<CarriableComponent>(target, out var carriable))
        {
            carriable.CarriedBy = null;
            Dirty(target, carriable);
            RemCompDeferred<BeingCarriedComponent>(target);
            _actionBlocker.UpdateCanMove(target);
        }
    }

    private void OnDropHandItems(EntityUid uid, ActiveCarrierComponent component, DropHandItemsEvent args)
    {
        Drop(uid);
    }

    private void OnVirtualItemDeleted(EntityUid uid, CarrierComponent component, VirtualItemDeletedEvent args)
    {
        if (!_transitioning.Contains(uid) && component.Carrying == args.BlockingEntity)
            Drop(uid, component);
    }

    private void OnCarriedBuckleAttempt(EntityUid uid, BeingCarriedComponent component, ref BuckleAttemptEvent args)
    {
        if (TryComp<CarriableComponent>(uid, out var carriable) && carriable.CarriedBy is { } carrier)
            Drop(carrier);
    }

    #endregion

    #region State Validation

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextValidation)
            return;
        _nextValidation = _timing.CurTime + ValidationInterval;

        var query = EntityQueryEnumerator<ActiveCarrierComponent, CarrierComponent>();
        while (query.MoveNext(out var uid, out _, out var carrier))
        {
            if (_transitioning.Contains(uid))
                continue;

            if (carrier.Carrying is not { } target || !Exists(target) || Terminating(target))
            {
                CleanOrphanedCarryState(uid, carrier);
                continue;
            }

            if (Transform(target).ParentUid != uid)
            {
                // Target escaped the carrier. Try a normal Drop first, then
                // fall back to orphan cleanup if Drop couldn't fully resolve it.
                Drop(uid, carrier);
                if (HasComp<ActiveCarrierComponent>(uid))
                    CleanOrphanedCarryState(uid, carrier);
            }
        }
    }

    /// <summary>
    /// Fallback cleanup for when <see cref="Drop"/> can't fully resolve a broken carry,
    /// such as when the target no longer exists or Carrying was already nulled but
    /// marker components and virtual items are still hanging around.
    /// </summary>
    private void CleanOrphanedCarryState(EntityUid uid, CarrierComponent carrier)
    {
        var target = carrier.Carrying;

        carrier.Carrying = null;
        Dirty(uid, carrier);

        if (target != null && Exists(target.Value) && !Terminating(target.Value))
        {
            if (TryComp<CarriableComponent>(target.Value, out var carriable))
            {
                carriable.CarriedBy = null;
                Dirty(target.Value, carriable);
            }

            RemCompDeferred<BeingCarriedComponent>(target.Value);

            _transitioning.Add(uid);
            _virtualItem.DeleteInHandsMatching(uid, target.Value);
            _transitioning.Remove(uid);

            if (!Terminating(uid))
            {
                var xform = Transform(target.Value);
                if (xform.ParentUid == uid)
                {
                    _transform.PlaceNextTo((target.Value, xform), (uid, Transform(uid)));
                    xform.ActivelyLerping = false;
                }
            }

            _joints.RefreshRelay(target.Value);
            _actionBlocker.UpdateCanMove(target.Value);

            if (!_mobState.IsIncapacitated(target.Value) && !HasComp<KnockedDownComponent>(target.Value))
                _standing.Stand(target.Value);
        }

        RemComp<ActiveCarrierComponent>(uid);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

        Log.Warning($"Cleaned orphaned carry state on {ToPrettyString(uid)}");
    }

    #endregion
}

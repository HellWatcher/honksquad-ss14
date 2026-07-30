using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Carrying.Components;
using Content.Shared.RussStation.Carrying.Events;
using Content.Shared.DoAfter;
using Content.Shared.RussStation.EscalatedGrab;
using Content.Shared.RussStation.EscalatedGrab.Systems;
using Content.Shared.RussStation.Markers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.Shared.RussStation.Carrying.Systems;

/// <summary>
/// Core of the carry mechanic: deciding whether a carry is allowed
/// (<see cref="CanCarry"/>), wiring one up (<see cref="Carry"/>) and tearing it
/// down (<see cref="Drop"/>). The surrounding concerns are split across partials:
/// <see cref="AddCarryVerb"/> and friends in <c>.Verbs.cs</c>, drag-drop in
/// <c>.DragDrop.cs</c>, the auto-drop interruption handlers in <c>.AutoDrop.cs</c>,
/// and the symmetric teardown in <c>.Cleanup.cs</c>.
/// </summary>
public abstract partial class SharedCarryingSystem : PairedMarkerSystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedEscalatedGrabSystem _grab = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    // Carriers currently setting up or tearing down a carry. While in this set,
    // OnVirtualItemDeleted won't call Drop(), preventing double-drop cascades.
    private readonly HashSet<EntityUid> _transitioning = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CarrierComponent, CarryDoAfterEvent>(OnCarryDoAfter);
        SubscribeLocalEvent<BeingCarriedComponent, Content.Shared.Pulling.Events.BeingPulledAttemptEvent>(OnCarriedPullAttempt);
        SubscribeLocalEvent<ActiveCarrierComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<BeingCarriedComponent, UpdateCanMoveEvent>(OnCarriedCanMove);

        InitializeVerbs();
        InitializeDragDrop();
        InitializeAutoDrop();
        InitializeCleanup();
    }

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
    /// Block third-party pull attempts on a carried entity. Pull-while-carried races
    /// with the carry's reparent and drop hooks (the puller's virtual item handles
    /// don't get fixed up cleanly), so the prying interrupt verb is the supported way
    /// to intervene. The carrier's own pull-then-carry handoff is already cleared
    /// during Carry() before BeingCarriedComponent gets attached, so this only blocks
    /// new pulls that start after the carry is in progress.
    /// </summary>
    private void OnCarriedPullAttempt(EntityUid uid, BeingCarriedComponent component, Content.Shared.Pulling.Events.BeingPulledAttemptEvent args)
    {
        args.Cancel();
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
            Log.Warning("Failed to spawn first carry virtual item on {Carrier}", ToPrettyString(carrier));
        if (!_virtualItem.TrySpawnVirtualItemInHand(target, carrier))
            Log.Warning("Failed to spawn second carry virtual item on {Carrier}", ToPrettyString(carrier));

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
}

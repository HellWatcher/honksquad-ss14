using Content.Shared.RussStation.Carrying.Components;
using Content.Shared.RussStation.Carrying.Events;
using Content.Shared.Hands;
using Content.Shared.Standing;
using Content.Shared.Stunnable;

namespace Content.Shared.RussStation.Carrying.Systems;

// Teardown. Removing either marker is the single point of truth for ending a carry:
// the marker shutdowns mirror the removal to their partner and OnBeingCarriedShutdown
// performs the visible cleanup, broken out into per-side helpers.
public abstract partial class SharedCarryingSystem
{
    private void InitializeCleanup()
    {
        SubscribeLocalEvent<BeingCarriedComponent, ComponentShutdown>(OnBeingCarriedShutdown);
        SubscribeLocalEvent<ActiveCarrierComponent, ComponentShutdown>(OnActiveCarrierShutdown);
        SubscribeLocalEvent<ActiveCarrierComponent, DropHandItemsEvent>(OnDropHandItems);
        SubscribeLocalEvent<CarrierComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted);
    }

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
            TeardownCarrier(carrier, uid);

        if (!Terminating(uid))
        {
            if (Exists(carrier) && !Terminating(carrier))
                Reparent(uid, carrier);

            RestorePosture(uid, carrier);
        }

        var ev = new CarryStoppedEvent(carrier, uid);
        if (Exists(carrier))
            RaiseLocalEvent(carrier, ref ev);
        RaiseLocalEvent(uid, ref ev);
    }

    /// <summary>
    /// Carrier-side teardown: deletes the carry's virtual hand items, restores the
    /// carrier's movement speed and shows its drop popup. The <see cref="_transitioning"/>
    /// guard stops the virtual-item deletion from re-entering <see cref="Drop"/>.
    /// </summary>
    private void TeardownCarrier(EntityUid carrier, EntityUid target)
    {
        _transitioning.Add(carrier);
        _virtualItem.DeleteInHandsMatching(carrier, target);
        _transitioning.Remove(carrier);
        _movementSpeed.RefreshMovementSpeedModifiers(carrier);

        _popup.PopupClient(Loc.GetString("carrying-drop-carrier", ("target", target)), carrier, carrier);
    }

    /// <summary>
    /// Detaches the carried entity from the carrier and places it back into the world
    /// next to them, ending the carry lerp. No-op if it's no longer parented to the carrier.
    /// </summary>
    private void Reparent(EntityUid target, EntityUid carrier)
    {
        var targetXform = Transform(target);
        if (targetXform.ParentUid != carrier)
            return;

        _transform.PlaceNextTo((target, targetXform), (carrier, Transform(carrier)));
        targetXform.ActivelyLerping = false;
        Dirty(target, targetXform);
    }

    /// <summary>
    /// Target-side teardown: re-enables movement, stands the dropped entity back up
    /// (unless it's incapacitated or knocked down) and shows its drop popup.
    /// </summary>
    private void RestorePosture(EntityUid target, EntityUid carrier)
    {
        _joints.RefreshRelay(target);
        _actionBlocker.UpdateCanMove(target);

        if (!_mobState.IsIncapacitated(target) && !HasComp<KnockedDownComponent>(target))
            _standing.Stand(target);

        _popup.PopupClient(Loc.GetString("carrying-drop-carried", ("carrier", carrier)), target, target);
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
}

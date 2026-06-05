using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Carrying.Components;
using Content.Shared.Mobs;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;

namespace Content.Shared.RussStation.Carrying.Systems;

// Auto-drop: a carry ends the moment either party hits a state it can't survive
// (the carried one stands/buckles/wakes, the carrier is stunned/downed/critical,
// either is stuffed into a container, ...). Most of these are pure "drop now"
// reactions differing only by event type, so they're wired through the generic
// SubscribeCarried*/SubscribeCarrier* helpers below; only the conditional ones
// keep a hand-written handler.
public abstract partial class SharedCarryingSystem
{
    private void InitializeAutoDrop()
    {
        // Carried-side interruptions: drop whoever is carrying this entity.
        SubscribeCarriedDrop<StoodEvent>();
        SubscribeCarriedDrop<EntGotInsertedIntoContainerMessage>();
        SubscribeCarriedDropRef<BuckledEvent>();
        SubscribeCarriedDropRef<BuckleAttemptEvent>();

        // Carrier-side interruptions: drop the carry this entity is performing.
        SubscribeCarrierDrop<EntGotInsertedIntoContainerMessage>();
        SubscribeCarrierDropRef<StunnedEvent>();
        SubscribeCarrierDropRef<DownedEvent>();

        // Conditional interruptions that can't reduce to an unconditional drop.
        SubscribeLocalEvent<BeingCarriedComponent, MobStateChangedEvent>(OnCarriedMobStateChanged);
        SubscribeLocalEvent<ActiveCarrierComponent, MobStateChangedEvent>(OnCarrierMobStateChanged);
        SubscribeLocalEvent<BeingCarriedComponent, EntParentChangedMessage>(OnCarriedParentChanged);
    }

    /// <summary>
    /// Ends the carry on behalf of an external interruption (a state change, a
    /// container insert, ...). A named seam over <see cref="Drop"/> so the auto-drop
    /// subscriptions all funnel through one entry point.
    /// </summary>
    private void DropFromInterrupt(EntityUid carrier) => Drop(carrier);

    /// <summary>Drop the carry when a by-value <typeparamref name="TEvent"/> fires on the carried entity.</summary>
    private void SubscribeCarriedDrop<TEvent>() where TEvent : notnull
        => SubscribeLocalEvent<BeingCarriedComponent, TEvent>(
            (EntityUid uid, BeingCarriedComponent comp, TEvent args) => DropFromInterrupt(comp.Carrier));

    /// <summary>Drop the carry when a by-ref <typeparamref name="TEvent"/> fires on the carried entity.</summary>
    private void SubscribeCarriedDropRef<TEvent>() where TEvent : notnull
        => SubscribeLocalEvent<BeingCarriedComponent, TEvent>(
            (EntityUid uid, BeingCarriedComponent comp, ref TEvent args) => DropFromInterrupt(comp.Carrier));

    /// <summary>Drop the carry when a by-value <typeparamref name="TEvent"/> fires on the carrier.</summary>
    private void SubscribeCarrierDrop<TEvent>() where TEvent : notnull
        => SubscribeLocalEvent<ActiveCarrierComponent, TEvent>(
            (EntityUid uid, ActiveCarrierComponent comp, TEvent args) => DropFromInterrupt(uid));

    /// <summary>Drop the carry when a by-ref <typeparamref name="TEvent"/> fires on the carrier.</summary>
    private void SubscribeCarrierDropRef<TEvent>() where TEvent : notnull
        => SubscribeLocalEvent<ActiveCarrierComponent, TEvent>(
            (EntityUid uid, ActiveCarrierComponent comp, ref TEvent args) => DropFromInterrupt(uid));

    private void OnCarriedMobStateChanged(EntityUid uid, BeingCarriedComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            DropFromInterrupt(component.Carrier);
    }

    private void OnCarrierMobStateChanged(EntityUid uid, ActiveCarrierComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            DropFromInterrupt(uid);
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
            DropFromInterrupt(component.Carrier);
    }
}

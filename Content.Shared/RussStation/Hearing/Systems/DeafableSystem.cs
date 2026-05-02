using Content.Shared.Inventory;
using Content.Shared.Rejuvenate;

namespace Content.Shared.RussStation.Hearing.Systems;

/// <summary>
/// Manages the IsDeaf state on <see cref="DeafableComponent"/> by raising
/// <see cref="CanHearAttemptEvent"/>. Sources of deafness (cybernetic ears, missing ears,
/// flashbangs, etc.) live in their own systems and subscribe to the attempt event.
/// </summary>
public sealed class DeafableSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeafableComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<DeafableComponent> ent, ref RejuvenateEvent args)
    {
        UpdateIsDeaf((ent.Owner, (DeafableComponent?) ent.Comp));
    }

    public void UpdateIsDeaf(Entity<DeafableComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        if (TerminatingOrDeleted(entity.Owner))
            return;

        var old = entity.Comp.IsDeaf;

        var ev = new CanHearAttemptEvent();
        RaiseLocalEvent(entity.Owner, ev);
        entity.Comp.IsDeaf = ev.Deaf;

        if (old == entity.Comp.IsDeaf)
            return;

        var changeEv = new DeafnessChangedEvent(entity.Comp.IsDeaf);
        RaiseLocalEvent(entity.Owner, ref changeEv);
        Dirty(entity);
    }
}

[ByRefEvent]
public record struct DeafnessChangedEvent(bool Deaf);

/// <summary>
/// Raised directed at an entity to check whether it can currently hear.
/// Cancel to make the entity deaf.
/// </summary>
public sealed class CanHearAttemptEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public bool Deaf => Cancelled;
    public SlotFlags TargetSlots => SlotFlags.EARS | SlotFlags.HEAD | SlotFlags.MASK;
}

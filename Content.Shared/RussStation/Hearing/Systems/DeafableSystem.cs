using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Inventory;
using Content.Shared.Rejuvenate;
using Content.Shared.RussStation.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Hearing.Systems;

/// <summary>
/// Manages the IsDeaf state on <see cref="DeafableComponent"/> by raising
/// <see cref="CanHearAttemptEvent"/>. Sources of deafness (cybernetic ears, missing ears,
/// flashbangs, etc.) live in their own systems and subscribe to the attempt event.
///
/// Also handles the body-organ ear-tracking path: a body with an Advanced
/// <see cref="CyberneticEarsComponent"/> trumps every other deafness source (Uncancel),
/// while a body with no Ears-category organ at all is unconditionally deaf (Cancel).
/// </summary>
public sealed class DeafableSystem : EntitySystem
{
    private static readonly ProtoId<OrganCategoryPrototype> EarsCategory = "Ears";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeafableComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<BodyComponent, CanHearAttemptEvent>(OnCanHearAttempt);
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

    private void OnCanHearAttempt(EntityUid uid, BodyComponent body, CanHearAttemptEvent args)
    {
        if (body.Organs == null)
            return;

        var hasEars = false;
        foreach (var organ in body.Organs.ContainedEntities)
        {
            // Advanced cybernetic ears trump any other deafness source (earmuffs, flashbangs, etc).
            if (HasComp<CyberneticEarsComponent>(organ))
            {
                args.Uncancel();
                return;
            }

            if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category == EarsCategory)
                hasEars = true;
        }

        if (!hasEars)
            args.Cancel();
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

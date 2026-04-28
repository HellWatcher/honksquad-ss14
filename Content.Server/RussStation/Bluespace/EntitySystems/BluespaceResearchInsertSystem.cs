// HONK - Issue #302 follow-up: see BluespaceResearchInsertComponent for design.

using Content.Server.Popups;
using Content.Server.Research.Systems;
using Content.Server.RussStation.Bluespace.Components;
using Content.Shared.Interaction;
using Content.Shared.Research.Components;
using Content.Shared.Stacks;

namespace Content.Server.RussStation.Bluespace.EntitySystems;

public sealed class BluespaceResearchInsertSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BluespaceResearchInsertComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<BluespaceResearchInsertComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (args.Target is not { } target || !TryComp<ResearchServerComponent>(target, out var server))
            return;

        // Stack-aware consumption: take one off the stack, credit one crystal's worth of
        // points. Stacked entities without StackComponent are treated as single-use.
        if (TryComp<StackComponent>(ent.Owner, out var stack))
        {
            if (!_stack.TryUse((ent.Owner, stack), 1))
                return;
        }
        else
        {
            QueueDel(ent.Owner);
        }

        _research.ModifyServerPoints(target, ent.Comp.Points, server);
        _popup.PopupEntity(
            Loc.GetString("research-disk-inserted", ("points", ent.Comp.Points)),
            target,
            args.User);

        args.Handled = true;
    }
}

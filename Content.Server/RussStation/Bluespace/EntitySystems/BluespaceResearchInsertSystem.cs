// HONK - Issue #302 follow-up: see BluespaceResearchInsertComponent for design.

using Content.Server.Popups;
using Content.Server.Research.Systems;
using Content.Server.RussStation.Bluespace.Components;
using Content.Shared.Interaction;
using Content.Shared.Research.Components;
using Content.Shared.Stacks;

namespace Content.Server.RussStation.Bluespace.EntitySystems;

public sealed partial class BluespaceResearchInsertSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private SharedStackSystem _stack = default!;

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

        // Consume the whole stack in one interaction and credit Points per
        // crystal. Non-stack callers fall through to a single-shot QueueDel
        // crediting one crystal's worth.
        var consumed = 1;
        if (TryComp<StackComponent>(ent.Owner, out var stack))
        {
            consumed = stack.Count;
            if (consumed <= 0 || !_stack.TryUse((ent.Owner, stack), consumed))
                return;
        }
        else
        {
            QueueDel(ent.Owner);
        }

        var points = ent.Comp.Points * consumed;
        _research.ModifyServerPoints(target, points, server);
        _popup.PopupEntity(
            Loc.GetString("research-disk-inserted", ("points", points)),
            target,
            args.User);

        args.Handled = true;
    }
}

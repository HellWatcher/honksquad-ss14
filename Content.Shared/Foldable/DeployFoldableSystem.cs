using Content.Shared.Construction.EntitySystems;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Foldable;

public sealed class DeployFoldableSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly FoldableSystem _foldable = default!;
    [Dependency] private readonly AnchorableSystem _anchorable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeployFoldableComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DeployFoldableComponent, CanDragEvent>(OnCanDrag);
        SubscribeLocalEvent<DeployFoldableComponent, DragDropDraggedEvent>(OnDragDropDragged);
        SubscribeLocalEvent<DeployFoldableComponent, CanDropDraggedEvent>(OnCanDropDragged);

        //HONK START - Drag-drop foldable onto self to fold and pick up
        SubscribeLocalEvent<HandsComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<HandsComponent, DragDropTargetEvent>(OnDropTarget);
        //HONK END
    }

    private void OnCanDropDragged(Entity<DeployFoldableComponent> ent, ref CanDropDraggedEvent args)
    {
        if (args.User != args.Target)
            return;

        args.Handled = true;
        args.CanDrop = true;
    }

    private void OnDragDropDragged(Entity<DeployFoldableComponent> ent, ref DragDropDraggedEvent args)
    {
        if (!TryComp<FoldableComponent>(ent, out var foldable)
            || !_foldable.TrySetFolded(ent, foldable, true, args.User))
            return;

        _hands.PickupOrDrop(args.User, ent.Owner);

        args.Handled = true;
    }

    private void OnCanDrag(Entity<DeployFoldableComponent> ent, ref CanDragEvent args)
    {
        if (!TryComp<FoldableComponent>(ent, out var foldable)
            || foldable.IsFolded)
            return;

        args.Handled = true;
    }

    //HONK START - Drag-drop foldable onto self to fold and pick up
    private void OnCanDropTarget(EntityUid uid, HandsComponent component, ref CanDropTargetEvent args)
    {
        if (args.User != uid)
            return;

        if (!HasComp<DeployFoldableComponent>(args.Dragged))
            return;

        if (!TryComp<FoldableComponent>(args.Dragged, out var foldable) || foldable.IsFolded)
            return;

        args.Handled = true;
        args.CanDrop = true;
    }

    private void OnDropTarget(EntityUid uid, HandsComponent component, ref DragDropTargetEvent args)
    {
        if (args.User != uid)
            return;

        if (!TryComp<FoldableComponent>(args.Dragged, out var foldable)
            || !_foldable.TrySetFolded(args.Dragged, foldable, true, args.User))
            return;

        _hands.PickupOrDrop(args.User, args.Dragged);
        args.Handled = true;
    }
    //HONK END

    private void OnAfterInteract(Entity<DeployFoldableComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // Don't do anything unless you clicked on the floor.
        if (args.Target.HasValue)
            return;

        if (!TryComp<FoldableComponent>(ent, out var foldable))
            return;

        if (!TryComp(ent.Owner, out PhysicsComponent? anchorBody)
            || !_anchorable.TileFree(args.ClickLocation, anchorBody))
        {
            _popup.PopupPredicted(Loc.GetString("foldable-deploy-fail", ("object", ent)), ent, args.User);
            return;
        }

        if (!TryComp(args.User, out HandsComponent? hands)
            || !_hands.TryDrop((args.User, hands), args.Used, targetDropLocation: args.ClickLocation))
            return;

        if (!_foldable.TrySetFolded(ent, foldable, false, args.User))
        {
            _hands.TryPickup(args.User, args.Used, handsComp: hands);
            return;
        }

        args.Handled = true;
    }
}

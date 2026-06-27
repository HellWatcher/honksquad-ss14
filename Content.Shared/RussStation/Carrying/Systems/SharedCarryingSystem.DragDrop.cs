using Content.Shared.RussStation.Carrying.Components;
using Content.Shared.DragDrop;

namespace Content.Shared.RussStation.Carrying.Systems;

// Adapts SS14's drag-drop interaction onto the carry do-after: dragging a valid
// carriable onto yourself is equivalent to invoking the carry verb.
public abstract partial class SharedCarryingSystem
{
    private void InitializeDragDrop()
    {
        SubscribeLocalEvent<CarriableComponent, DragDropDraggedEvent>(OnDragDropDragged);
        SubscribeLocalEvent<CarriableComponent, CanDropDraggedEvent>(OnCanDropDragged);
        SubscribeLocalEvent<CarrierComponent, CanDropTargetEvent>(OnCanDropTarget);
    }

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
}

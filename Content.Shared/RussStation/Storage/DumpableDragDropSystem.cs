using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Storage;

[Serializable, NetSerializable]
public sealed partial class DumpableDragDropDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates TargetCoordinates;

    private DumpableDragDropDoAfterEvent()
    {
    }

    public DumpableDragDropDoAfterEvent(NetCoordinates targetCoordinates)
    {
        TargetCoordinates = targetCoordinates;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>
///     Allows dragging a <see cref="DumpableComponent"/> entity onto any target
///     to dump its contents at the drop position.
/// </summary>
public sealed class DumpableDragDropSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<ItemComponent> _itemQuery;

    public override void Initialize()
    {
        base.Initialize();
        _itemQuery = GetEntityQuery<ItemComponent>();

        SubscribeLocalEvent<DumpableComponent, CanDragEvent>(OnCanDrag);
        SubscribeLocalEvent<DumpableComponent, CanDropDraggedEvent>(OnCanDrop);
        SubscribeLocalEvent<DumpableComponent, DragDropDraggedEvent>(OnDragDrop);
        SubscribeLocalEvent<DumpableComponent, DumpableDragDropDoAfterEvent>(OnDoAfter);
    }

    private void OnCanDrag(EntityUid uid, DumpableComponent comp, ref CanDragEvent args)
    {
        if (!HasComp<StorageComponent>(uid))
            return;

        args.Handled = true;
    }

    private void OnCanDrop(EntityUid uid, DumpableComponent comp, ref CanDropDraggedEvent args)
    {
        if (!TryComp<StorageComponent>(uid, out var storage))
            return;

        if (storage.Container.ContainedEntities.Count == 0)
            return;

        args.CanDrop = true;
        args.Handled = true;
    }

    private void OnDragDrop(EntityUid uid, DumpableComponent comp, ref DragDropDraggedEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<StorageComponent>(uid, out var storage))
            return;

        if (storage.Container.ContainedEntities.Count == 0)
            return;

        var delay = 0f;
        foreach (var entity in storage.Container.ContainedEntities)
        {
            if (_itemQuery.TryGetComponent(entity, out var itemComp)
                && _proto.Resolve(itemComp.Size, out var itemSize))
            {
                delay += itemSize.Weight;
            }
        }

        delay *= (float) comp.DelayPerItem.TotalSeconds * comp.Multiplier;

        // Capture the target's coordinates so items dump where the user dropped,
        // not where the storage entity is.
        var targetCoords = GetNetCoordinates(Transform(args.Target).Coordinates);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, delay,
            new DumpableDragDropDoAfterEvent(targetCoords), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            NeedHand = true,
        });

        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, DumpableComponent comp, DumpableDragDropDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<StorageComponent>(uid, out var storage))
            return;

        if (storage.Container.ContainedEntities.Count == 0)
            return;

        var targetCoords = GetCoordinates(args.TargetCoordinates);

        foreach (var entity in storage.Container.ContainedEntities.ToArray())
        {
            var offset = _random.NextVector2Box() / 4;
            _transform.SetCoordinates(entity, targetCoords.Offset(offset));
            _transform.SetLocalRotation(entity, _random.NextAngle());
        }

        args.Handled = true;
    }
}

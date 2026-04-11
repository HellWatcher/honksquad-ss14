using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;

namespace Content.Shared.RussStation.Pulling;

// Stops the pull when a player drops the virtual item representing a pulled entity.
// Relocated out of upstream PullingSystem.cs to keep rebase surface minimal (issue #441 P0.1).
public sealed class VirtualItemPullReleaseSystem : EntitySystem
{
    [Dependency] private readonly PullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VirtualItemComponent, DroppedEvent>(OnVirtualItemDropped);
    }

    private void OnVirtualItemDropped(EntityUid uid, VirtualItemComponent component, DroppedEvent args)
    {
        if (!TryComp<PullerComponent>(args.User, out var puller))
            return;

        if (puller.Pulling != component.BlockingEntity)
            return;

        if (TryComp(component.BlockingEntity, out PullableComponent? pullable))
            _pulling.TryStopPull(component.BlockingEntity, pullable, user: args.User);
    }
}

using Content.Shared.Lock;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Traits;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Traits;

public sealed class SkittishSystem : EntitySystem
{
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkittishComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<SkittishComponent> ent, ref StartCollideEvent args)
    {
        var uid = ent.Owner;
        var other = args.OtherEntity;

        // Don't trigger if already inside a container.
        if (_container.IsEntityInContainer(uid))
            return;

        // Only trigger when the player is sprinting, not walking.
        if (TryComp<InputMoverComponent>(uid, out var mover) && !mover.Sprinting)
            return;

        // The collided entity must be a closed storage.
        if (!TryComp<EntityStorageComponent>(other, out var storageComp) || storageComp.Open)
            return;

        // The entity must be able to fit inside.
        if (!_entityStorage.CanInsert(uid, other, storageComp))
            return;

        // If the container is locked, the player must have access.
        if (TryComp<LockComponent>(other, out var lockComp) && lockComp.Locked && !_lock.TryUnlock(other, uid, lockComp))
            return;

        // Play the open and close sounds back-to-back for effect.
        _audio.PlayPvs(storageComp.OpenSound, other);
        _audio.PlayPvs(storageComp.CloseSound, other);

        // Insert directly into the closed container.
        _entityStorage.Insert(uid, other, storageComp);

        // Block the internal-movement handler from immediately re-opening the storage due to the
        // lingering movement input that triggered this collision.
        storageComp.NextInternalOpenAttempt = _timing.CurTime + TimeSpan.FromSeconds(1.0);
        Dirty(other, storageComp);

        // Lock the container after entering.
        if (lockComp != null)
            _lock.Lock(other, null, lockComp);

        _popup.PopupEntity(Loc.GetString("skittish-hide", ("container", other)), uid, uid);
    }
}

using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.RussStation.Weapons.Ranged;

/// <summary>
/// Handles fork-specific action gun extensions: popup text and sound workaround.
/// Subscribes alongside upstream's ActionGunSystem without modifying it.
/// </summary>
/// <remarks>
/// The upstream action gun spawns the gun entity in nullspace (no world position),
/// so Audio.PlayPredicted from that entity produces no audible sound.
/// This system plays the sound from the mob entity instead.
/// </remarks>
public sealed class ActionGunExtSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionGunExtComponent, ActionGunShootEvent>(OnShoot);
        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
    }

    private void OnShoot(Entity<ActionGunExtComponent> ent, ref ActionGunShootEvent args)
    {
        if (ent.Comp.PopupText == null)
            return;

        var name = Comp<MetaDataComponent>(ent).EntityName;
        _popup.PopupPredicted(name + " " + ent.Comp.PopupText + "!", ent, ent, type: PopupType.Small);
    }

    /// <summary>
    /// After a successful gun shot, play the action gun's sound from the mob
    /// instead of the gun entity (which has no world position).
    /// </summary>
    private void OnGunShot(Entity<GunComponent> gun, ref GunShotEvent args)
    {
        if (!TryComp<ActionGunExtComponent>(args.User, out var ext))
            return;

        if (ext.OnShootSound == null)
            return;

        // Only fire when the shot is from the mob's action gun, not a regular
        // held weapon. Otherwise every humanoid plays the spit sound on any shot.
        if (!TryComp<ActionGunComponent>(args.User, out var actionGun) || actionGun.Gun != gun.Owner)
            return;

        _audio.PlayPredicted(ext.OnShootSound, args.User, actionGun.ActionEntity);
    }
}

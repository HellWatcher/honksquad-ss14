using Content.Shared.Actions;
using Content.Shared.Weapons.Ranged.Components;

//HONK START
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Content.Shared.Actions.Components;
//HONK END

namespace Content.Shared.Weapons.Ranged.Systems;

public sealed class ActionGunSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    //HONK START
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    //HONK

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionGunComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ActionGunComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ActionGunComponent, ActionGunShootEvent>(OnShoot);
    }

    private void OnMapInit(Entity<ActionGunComponent> ent, ref MapInitEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.Action))
            return;

        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        ent.Comp.Gun = Spawn(ent.Comp.GunProto);
    }

    private void OnShutdown(Entity<ActionGunComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Gun is { } gun)
            QueueDel(gun);
    }

    private void OnShoot(Entity<ActionGunComponent> ent, ref ActionGunShootEvent args)
    {
        //HONK START
        if (ent.Comp.PopupText != null)
            _popup.PopupPredicted(EntityManager.GetComponent<MetaDataComponent>(ent).EntityName + " " + ent.Comp.PopupText + "!", ent, ent, type: PopupType.Small);

        if (TryComp<GunComponent>(ent.Comp.Gun, out var gun))
        {
            if (_gun.AttemptShoot(ent, (ent.Comp.Gun.Value, gun), args.Target) == true)
                _audio.PlayPredicted(ent.Comp.OnShootSound, ent, ent.Comp.ActionEntity); //Fixes shooting sounds for action guns
        }
        //HONK END
    }
}


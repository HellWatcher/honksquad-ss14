using Content.Shared.Actions;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
//HONK START
using Robust.Shared.Audio;
//HONK END
namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Lets you shoot a gun using an action.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(ActionGunSystem))]
public sealed partial class ActionGunComponent : Component
{
    /// <summary>
    /// Action to create, must use <see cref="ActionGunShootEvent"/>.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Action = string.Empty;

    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Prototype of gun entity to spawn.
    /// Deleted when this component is removed.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId GunProto = string.Empty;

    [DataField]
    public EntityUid? Gun;

    //HONK START
    [DataField]
    public string? PopupText = null;

    [DataField]
    public SoundSpecifier? OnShootSound;
    //HONK END
}

/// <summary>
/// Action event for <see cref="ActionGunComponent"/> to shoot at a position.
/// </summary>
public sealed partial class ActionGunShootEvent : WorldTargetActionEvent;

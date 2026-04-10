using Robust.Shared.Audio;
using Robust.Shared.GameObjects;

namespace Content.Shared.RussStation.Weapons.Ranged;

/// <summary>
/// Fork extension for <see cref="Content.Shared.Weapons.Ranged.Components.ActionGunComponent"/>.
/// Adds popup text on shoot and a manual sound workaround.
/// The upstream action gun spawns the gun entity in nullspace, so
/// Audio.PlayPredicted from that entity produces no audible sound.
/// This component lets us play the sound from the mob instead.
/// </summary>
[RegisterComponent]
public sealed partial class ActionGunExtComponent : Component
{
    /// <summary>
    /// Optional popup text shown when firing (e.g. "spits").
    /// Displayed as "[EntityName] [PopupText]!".
    /// </summary>
    [DataField]
    public string? PopupText;

    /// <summary>
    /// Sound played from the mob on successful shot.
    /// Works around the upstream issue where the gun entity has no world position.
    /// </summary>
    [DataField]
    public SoundSpecifier? OnShootSound;
}

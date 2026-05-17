using Robust.Shared.Audio;

namespace Content.Shared.RussStation.Skillchips;

[RegisterComponent]
public sealed partial class SkillchipStationComponent : Component
{
    /// <summary>
    /// Container ID for the chip slot visible in the station's input tray.
    /// </summary>
    public const string ChipSlotId = "skillchip_slot";

    /// <summary>
    /// Container ID for the occupant standing inside the chair. Mirrors the
    /// medical scanner / cryo pod body-container pattern: the patient is hidden
    /// by containment, not seated via Strap.
    /// </summary>
    public const string BodyContainerId = "scanner-body";

    /// <summary>
    /// How long the implant or removal operation takes.
    /// </summary>
    [DataField]
    public TimeSpan OperationDuration = TimeSpan.FromSeconds(SkillchipsConstants.StationOperationSeconds);

    [DataField]
    public SoundSpecifier OperationStartSound = new SoundPathSpecifier("/Audio/Machines/scanning.ogg");

    [DataField]
    public SoundSpecifier OperationCompleteSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");
}

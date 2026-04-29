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
    /// How long the implant or removal operation takes.
    /// </summary>
    [DataField]
    public TimeSpan OperationDuration = TimeSpan.FromSeconds(15);

    [DataField]
    public SoundSpecifier OperationStartSound = new SoundPathSpecifier("/Audio/Machines/scanning.ogg");

    [DataField]
    public SoundSpecifier OperationCompleteSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");
}

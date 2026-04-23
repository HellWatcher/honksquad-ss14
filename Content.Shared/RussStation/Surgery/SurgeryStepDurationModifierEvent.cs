namespace Content.Shared.RussStation.Surgery;

/// <summary>
/// Raised on both the patient and the surgeon when a surgery step's duration is being computed.
/// Subscribers can multiply <see cref="Multiplier"/> by their own factor to speed a step up or slow
/// it down. Inspect <see cref="Step"/> if the subscriber only cares about specific presets or tool
/// qualities. The standard surface / drape / difficulty / tool-tier multipliers are applied
/// alongside this event, so anything added here stacks on top.
/// </summary>
[ByRefEvent]
public struct SurgeryStepDurationModifierEvent
{
    public SurgeryStep Step;
    public EntityUid Patient;
    public EntityUid? Surgeon;
    public float Multiplier;

    public SurgeryStepDurationModifierEvent(SurgeryStep step, EntityUid patient, EntityUid? surgeon)
    {
        Step = step;
        Patient = patient;
        Surgeon = surgeon;
        Multiplier = 1f;
    }
}

using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.EscalatedGrab.Components;

/// <summary>
/// Added by the Pushover quirk. Multiplies grab resist time, making it harder to break free.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PushoverComponent : Component
{
    [DataField]
    public float ResistTimeMultiplier = 1.5f;
}

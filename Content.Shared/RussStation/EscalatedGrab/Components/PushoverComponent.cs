using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.EscalatedGrab.Components;

/// <summary>
/// Added by the Pushover quirk. Multiplies grab resist time, making it harder to break free.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PushoverComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ResistTimeMultiplier = EscalatedGrabConstants.PushoverResistTimeMultiplier;
}

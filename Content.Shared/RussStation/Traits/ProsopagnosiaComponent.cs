using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Prevents the entity from recognizing faces. Other humanoids appear as
/// generic descriptions ("middle-aged man") instead of their actual names.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ProsopagnosiaComponent : Component
{
}

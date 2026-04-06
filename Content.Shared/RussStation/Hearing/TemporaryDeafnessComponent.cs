using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Hearing;

/// <summary>
/// Component applied by the TemporaryDeafness status effect.
/// While present, the entity cannot hear.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TemporaryDeafnessComponent : Component
{
}

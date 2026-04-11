using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// When this entity bumps into a closed container it can fit inside,
/// it automatically opens the container, enters it, closes it, and
/// locks it if the container supports locking.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SkittishComponent : Component
{
}

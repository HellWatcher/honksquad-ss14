using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Economy.Components;

/// <summary>
/// Tracks a player's speso balance for the current round.
/// Added to the player mob on spawn.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlayerBalanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Balance;
}

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

    /// <summary>
    /// Unique account number assigned at spawn. Used by ID cards and ATMs to reference this balance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string AccountNumber = string.Empty;

    /// <summary>
    /// PIN for ATM access. Only known to the owning player via the Memories panel.
    /// </summary>
    [DataField]
    public string Pin = string.Empty;
}

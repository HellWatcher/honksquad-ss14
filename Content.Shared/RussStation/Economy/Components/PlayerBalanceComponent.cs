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
    /// Unique hex account number assigned at spawn. Used by ID cards to reference this balance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string AccountNumber = string.Empty;

    /// <summary>
    /// The job prototype ID this player spawned with. Used by the payroll system to determine wage tier.
    /// </summary>
    [DataField]
    public string? JobId;
}

using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Memories;

/// <summary>
/// Stores private character memories (key-value pairs) visible only to the owning player.
/// Added to the player mob on spawn by systems that register memories.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MemoriesComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public Dictionary<string, string> Memories = new();
}

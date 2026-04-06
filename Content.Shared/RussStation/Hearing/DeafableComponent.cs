using Content.Shared.RussStation.Hearing.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Hearing;

/// <summary>
/// Component for entities that can be deafened.
/// Analogous to <see cref="Content.Shared.Eye.Blinding.Components.BlindableComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(DeafableSystem))]
public sealed partial class DeafableComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsDeaf;
}

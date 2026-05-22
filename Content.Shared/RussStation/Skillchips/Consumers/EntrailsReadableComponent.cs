using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Tags an organ entity as something the <c>entrails_reader</c> skillchip
/// capability can read on examine. Added fork-side to <c>OrganBase</c> so
/// every organ inherits the hook without us touching each species file.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EntrailsReadableComponent : Component
{
}

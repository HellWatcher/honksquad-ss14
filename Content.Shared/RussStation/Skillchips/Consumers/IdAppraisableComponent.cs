using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Tags an ID card entity as something the <c>id_appraisal</c> skillchip
/// capability can read on examine. Added fork-side to <c>IDCardStandard</c>
/// so every ID inherits the hook without us touching each preset card.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IdAppraisableComponent : Component
{
}

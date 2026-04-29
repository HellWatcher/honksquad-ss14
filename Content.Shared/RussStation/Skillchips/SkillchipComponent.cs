using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Skillchips;

/// <summary>
/// Marks an entity as a skillchip item that can be implanted into a brain.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSkillchipSystem))]
public sealed partial class SkillchipComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<SkillchipPrototype> ChipProto;
}

using Content.Shared.RussStation.Surgery.Systems; //HONK
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

/// <summary>
/// Marks an entity as being able to be inserted into an entity with <seealso cref="BodyComponent" />.
/// </summary>
/// <seealso cref="BodySystem" />
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
//HONK START - Fork systems need to read organ Category
[Access(typeof(BodySystem), typeof(SharedSurgerySystem), Other = AccessPermissions.ReadExecute)]
//HONK END
public sealed partial class OrganComponent : Component
{
    /// <summary>
    /// The body entity containing this organ, if any
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    /// <summary>
    /// What kind of organ is this, if any
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? Category;
}

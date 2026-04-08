using Robust.Shared.GameStates;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// An entity with this component cannot read or write.
/// Reading is blocked by preventing paper UI from opening.
/// Writing is blocked by <see cref="Content.Shared.Paper.BlockWritingComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IlliterateComponent : Component
{
}

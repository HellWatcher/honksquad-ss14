using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Fragments;

/// <summary>
/// Specific ui fragments need to inherit this class. The subclass is then used in yaml to tell a main ui to use it as a ui fragment.
/// </summary>
/// <example>
/// This is an example from the yaml definition from the notekeeper ui
/// <code>
/// - type: CartridgeUi
///     ui: !type:NotekeeperUi
/// </code>
/// </example>
[ImplicitDataDefinitionForInheritors]
public abstract partial class UIFragment
{
    public abstract Control GetUIFragmentRoot();

    /// <summary>
    /// Called when the cartridge UI is activated. May be called multiple times for the
    /// same program (e.g. PdaUpdateState inherits CartridgeLoaderUiState, so any PDA
    /// UI refresh re-triggers this). Implementations must be idempotent: use
    /// <c>_fragment ??= new MyFragment()</c>, not <c>_fragment = new MyFragment()</c>.
    /// Creating a new control here without re-attaching it to the UI tree causes the
    /// displayed fragment and the updated fragment to diverge silently.
    /// </summary>
    public abstract void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner);

    public abstract void UpdateState(BoundUserInterfaceState state);

}

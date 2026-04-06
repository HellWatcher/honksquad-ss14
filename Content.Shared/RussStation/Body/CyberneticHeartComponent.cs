using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// When attached to an organ inside a body, auto-injects epinephrine
/// into the host's bloodstream when they enter crit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticHeartComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Epinephrine";

    [DataField]
    public FixedPoint2 InjectAmount = FixedPoint2.New(10);

    /// <summary>
    /// Minimum time between auto-injections.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(120);

    /// <summary>
    /// When the last injection occurred. Tracked on the component so it
    /// survives across system updates.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? LastInjection;
}

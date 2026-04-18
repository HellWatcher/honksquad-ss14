using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.RussStation.MedicalScanner;

/// <summary>
/// Fork-side marker + live-update state for the reagent tab of the tabbed health analyzer UI.
/// Sits alongside the upstream <c>HealthAnalyzerComponent</c>; our system subscribes its events
/// off this component to avoid colliding with upstream's (component, event) subscription slot.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class HealthAnalyzerReagentScannerComponent : Component
{
    /// <summary>
    /// Entity currently being ticked for reagent updates. Pinned after a successful scan;
    /// cleared on drop, toggle-off, deletion, or mode-wide stop.
    /// </summary>
    [DataField]
    public EntityUid? ReagentScanTarget;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextReagentUpdate = TimeSpan.Zero;

    [DataField]
    public TimeSpan ReagentUpdateInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public float? MaxReagentScanRange = 2.5f;

    /// <summary>
    /// Edge flag mirroring upstream's <c>IsAnalyzerActive</c>. Only true while we are
    /// pushing live state; flipped false by <see cref="HealthAnalyzerReagentSystem"/>
    /// on out-of-range pause so the "paused" state ships exactly once.
    /// </summary>
    public bool IsReagentScanActive;
}

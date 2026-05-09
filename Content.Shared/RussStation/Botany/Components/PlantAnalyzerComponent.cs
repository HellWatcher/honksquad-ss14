using Content.Shared.RussStation.Botany;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.RussStation.Botany.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PlantAnalyzerComponent : Component
{
    [DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(PlantAnalyzerConstants.ScanDelaySeconds);

    [DataField]
    public SoundSpecifier? ScanningBeginSound;

    [DataField]
    public SoundSpecifier ScanningEndSound = new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg");

    /// <summary>
    /// Which plant/produce entity is currently being tracked for continuous updates.
    /// </summary>
    [DataField]
    public EntityUid? ScannedEntity;

    /// <summary>
    /// The delay between UI updates while scanning.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(PlantAnalyzerConstants.UpdateIntervalSeconds);

    /// <summary>
    /// When the next periodic update should be sent.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// Whether the analyzer is currently actively sending updates (i.e. target is in range).
    /// </summary>
    [DataField]
    public bool IsAnalyzerActive = false;

    /// <summary>
    /// Maximum range in tiles for continuous updates. Null means infinite range.
    /// </summary>
    [DataField]
    public float? MaxScanRange = PlantAnalyzerConstants.MaxScanRange;
}

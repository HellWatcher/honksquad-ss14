using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Hearing;

/// <summary>
/// Applied to a mob when basic cybernetic ears are installed. Causes partial
/// audio muffling (weaker than full deafness) via DeafAudioSystem.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HearingImpairmentComponent : Component
{
    /// <summary>
    /// Extra occlusion added to all audio sources. Lower than the full deaf
    /// value of 8 — audible but noticeably muffled.
    /// </summary>
    [DataField]
    public float OcclusionBonus = 3.5f;
}

using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Hearing;

/// <summary>
/// Applied to a mob when basic cybernetic ears are installed. Causes partial
/// audio muffling (weaker than full deafness) via DeafAudioSystem on the client,
/// so the value has to network.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HearingImpairmentComponent : Component
{
    /// <summary>
    /// Extra occlusion added to all audio sources. Lower than the full deaf
    /// value of 8 — audible but noticeably muffled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OcclusionBonus = HearingImpairmentConstants.DefaultOcclusionBonus;
}

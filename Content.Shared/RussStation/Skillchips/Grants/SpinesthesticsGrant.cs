using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Shared.RussStation.Skillchips.Grants;

/// <summary>
/// Adds SpinesthesticsComponent to the mob, granting Spin and Flip emote effects at the cost of chip degradation.
/// </summary>
[Serializable]
public sealed partial class SpinesthesticsGrant : SkillchipGrant
{
    public override void Apply(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.EnsureMobComponent<SpinesthesticsComponent>(mob);

    public override void Revert(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.RemoveMobComponent<SpinesthesticsComponent>(mob);
}

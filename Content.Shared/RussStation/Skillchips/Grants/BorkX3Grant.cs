using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Shared.RussStation.Skillchips.Grants;

/// <summary>
/// Adds BorkX3Component to the mob, granting kitchen CQC bonuses.
/// </summary>
[Serializable]
public sealed partial class BorkX3Grant : SkillchipGrant
{
    public override void Apply(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.EnsureMobComponent<BorkX3Component>(mob);

    public override void Revert(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.RemoveMobComponent<BorkX3Component>(mob);
}

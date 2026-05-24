using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Shared.RussStation.Skillchips.Grants;

/// <summary>
/// Adds BulletDodgeComponent to the mob, granting a brief projectile-deflect window on trigger emote.
/// </summary>
[Serializable]
public sealed partial class BulletDodgeGrant : SkillchipGrant
{
    public override void Apply(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.EnsureMobComponent<BulletDodgeComponent>(mob);

    public override void Revert(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.RemoveMobComponent<BulletDodgeComponent>(mob);
}

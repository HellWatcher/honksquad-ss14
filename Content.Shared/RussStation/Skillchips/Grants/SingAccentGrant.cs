using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Shared.RussStation.Skillchips.Grants;

/// <summary>
/// Adds a SingAccentComponent to the mob, causing speech to be reshaped into song.
/// </summary>
[Serializable]
public sealed partial class SingAccentGrant : SkillchipGrant
{
    public override void Apply(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.EnsureMobComponent<SingAccentComponent>(mob);

    public override void Revert(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.RemoveMobComponent<SingAccentComponent>(mob);
}

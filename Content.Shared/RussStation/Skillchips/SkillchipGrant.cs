using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Skillchips;

/// <summary>
/// Base class for all grants a skillchip can apply to the mob it is implanted in.
/// Subclass and implement Apply/Revert to add new grant types.
/// </summary>
[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public abstract partial class SkillchipGrant
{
    public abstract void Apply(EntityUid mob, EntityUid brain, SharedSkillchipSystem system);
    public abstract void Revert(EntityUid mob, EntityUid brain, SharedSkillchipSystem system);
}

/// <summary>
/// Adds a prototyped action to the mob's action bar.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ActionGrant : SkillchipGrant
{
    [DataField(required: true)]
    public EntProtoId ActionProto;

    public override void Apply(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.ApplyActionGrant(mob, brain, ActionProto);

    public override void Revert(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.RevertActionGrant(mob, brain, ActionProto);
}

/// <summary>
/// Registers a capability tag on the holder's SkillchipHolderComponent.
/// Other systems query it via SkillchipSystem.HasCapability(mob, tag).
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CapabilityTagGrant : SkillchipGrant
{
    [DataField(required: true)]
    public string Tag = string.Empty;

    public override void Apply(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.ApplyCapabilityTag(brain, Tag);

    public override void Revert(EntityUid mob, EntityUid brain, SharedSkillchipSystem system)
        => system.RevertCapabilityTag(brain, Tag);
}

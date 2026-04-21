using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Medical;

/// <summary>
/// A manual resuscitator (bag valve mask) used to stave off suffocation on
/// incapacitated patients. Repeats its do-after automatically while the
/// target is still critical and has Asphyxiation damage above
/// <see cref="StopThreshold"/>, mirroring SS13 CPR.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ManualResuscitatorComponent : Component
{
    /// <summary>
    /// Damage applied to the target each cycle. Typically negative Asphyxiation
    /// to remove oxyloss.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public DamageSpecifier Heal = default!;

    /// <summary>
    /// How long one squeeze cycle takes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DoAfterDuration = MedicalConstants.ResuscitatorDoAfterDuration;

    /// <summary>
    /// The resuscitator stops repeating once the target's Asphyxiation damage is
    /// at or below this value. Above zero so it doesn't cycle forever on
    /// fully-healed patients whose damage hovers at a trivial amount.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 StopThreshold = MedicalConstants.ResuscitatorStopThreshold;

    /// <summary>
    /// If true, the resuscitator only engages on critical-state patients. Dead
    /// or alive targets are rejected.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CriticalOnly = true;

    /// <summary>
    /// Sound played at the start of each squeeze cycle.
    /// </summary>
    [DataField]
    public SoundSpecifier? SqueezeSound =
        new SoundPathSpecifier("/Audio/Effects/pop.ogg");
}

/// <summary>
/// DoAfterEvent raised when one resuscitator cycle finishes. Repeated
/// automatically by <c>ManualResuscitatorSystem</c> while the target still
/// needs oxygen.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ManualResuscitatorDoAfterEvent : SimpleDoAfterEvent;

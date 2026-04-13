using Content.Shared.Movement.Pulling.Components;
using Content.Shared.RussStation.Carrying.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.EscalatedGrab.Components;

/// <summary>
/// Added to a puller when their grab escalates beyond a standard pull.
/// Tracks the current <see cref="GrabStage"/> and target entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrabStateComponent : Component
{
    /// <summary>
    /// Entity being held in an escalated grab (physically restrained at a tier).
    /// One of three independent "holding" relationships on a mob, alongside
    /// <see cref="PullerComponent.Pulling"/> (standard drag-pull) and
    /// <see cref="CarrierComponent.Carrying"/> (fireman carry / lift).
    /// A single entity may legitimately participate in more than one at the same time.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Target;

    [DataField, AutoNetworkedField]
    public GrabStage Stage = GrabStage.Aggressive;

}

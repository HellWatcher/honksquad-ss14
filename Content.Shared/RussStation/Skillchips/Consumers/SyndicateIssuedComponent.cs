using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Marks an ID card prototype as Syndicate-issued (genuine syndicate cards
/// and agent IDs). Read by the GENUINE ID Appraisal Now! skillchip consumer
/// as the "not from any console you've ever seen" signal. Applied via HONK
/// markers to the AgentIDCard and SyndicateIDCard families upstream.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SyndicateIssuedComponent : Component
{
}

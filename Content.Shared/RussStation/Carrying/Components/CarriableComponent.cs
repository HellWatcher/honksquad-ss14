using Content.Shared.RussStation.Carrying.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Carrying.Components;

/// <summary>
/// Indicates this entity can be fireman carried (permission tag).
/// The active relationship lives on <see cref="BeingCarriedComponent"/>;
/// this component carries no per-carry state.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCarryingSystem))]
public sealed partial class CarriableComponent : Component;

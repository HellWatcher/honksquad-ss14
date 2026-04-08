using Robust.Shared.GameObjects;

namespace Content.Shared.RussStation.Wounds;

/// <summary>
/// Raised on an entity after wound processing from a damage hit.
/// Used by WoundEffectsSystem for item-drop logic without
/// conflicting with SharedWoundSystem's DamageChangedEvent subscription.
/// </summary>
public sealed class WoundsDamagedEvent : EntityEventArgs;

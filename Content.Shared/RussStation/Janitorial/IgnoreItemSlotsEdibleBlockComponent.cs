namespace Content.Shared.RussStation.Janitorial;

/// <summary>
/// Marker: skip upstream's "has stored items, can't drink" cancellation in
/// <c>IngestionSystem.OnItemSlotsEdible</c> for this entity. Used by the janitorial trolley so
/// drinking from its bucket works even when a mop or plunger is stowed in the other slots.
/// </summary>
[RegisterComponent]
public sealed partial class IgnoreItemSlotsEdibleBlockComponent : Component
{
}

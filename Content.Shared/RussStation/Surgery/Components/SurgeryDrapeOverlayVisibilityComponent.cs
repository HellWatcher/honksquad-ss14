namespace Content.Shared.RussStation.Surgery.Components;

/// <summary>
/// Marker on a drape overlay entity. A client system keeps the overlay's sprite rotation in sync
/// with the parent mob's body angle (lying down vs standing) and forces the south RSI state so the
/// art renders even when the strap underneath the patient is rotated to a non-south direction.
/// For standing mobs the system clears the override and lets the 4-direction RSI's south-only
/// content hide the overlay from the back / sides on its own.
/// </summary>
[RegisterComponent]
public sealed partial class SurgeryDrapeOverlayVisibilityComponent : Component
{
}

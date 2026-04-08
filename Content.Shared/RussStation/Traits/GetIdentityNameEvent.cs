namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Raised on the VIEWER entity when resolving another entity's identity name.
/// Allows viewer-side components to override how they perceive names.
/// </summary>
[ByRefEvent]
public record struct GetIdentityNameEvent(EntityUid Target, string Name, bool Handled = false);

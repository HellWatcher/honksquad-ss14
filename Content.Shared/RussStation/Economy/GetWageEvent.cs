namespace Content.Shared.RussStation.Economy;

/// <summary>
/// Raised on an entity before depositing their payroll wage.
/// Subscribers can modify the wage amount.
/// </summary>
[ByRefEvent]
public record struct GetWageEvent(int Wage);

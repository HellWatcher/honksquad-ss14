namespace Content.Shared.RussStation.Surgery;

/// <summary>
/// Difficulty tier for a surgical procedure, affecting step duration.
/// </summary>
public enum SurgeryDifficulty : byte
{
    Minor,
    Standard,
    Major,
    Critical,
}

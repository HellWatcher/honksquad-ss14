namespace Content.Shared.RussStation.Medical;

public static class MedicalConstants
{
    /// <summary>
    /// Asphyxiation damage threshold at/below which the manual resuscitator stops
    /// repeating. Kept above zero so it doesn't cycle forever on fully-healed
    /// patients whose damage hovers at a trivial amount.
    /// </summary>
    public const int ResuscitatorStopThreshold = 10;

    public static readonly TimeSpan ResuscitatorDoAfterDuration = TimeSpan.FromSeconds(3);
}

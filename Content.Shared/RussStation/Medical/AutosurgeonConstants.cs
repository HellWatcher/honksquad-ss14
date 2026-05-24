namespace Content.Shared.RussStation.Medical;

/// <summary>
/// Tunable defaults for autosurgeon installation timings.
/// </summary>
public static class AutosurgeonConstants
{
    /// <summary>Default doAfter time when an autosurgeon is used on another patient.</summary>
    public static readonly TimeSpan DefaultInstallTime = TimeSpan.FromSeconds(5);

    /// <summary>Default doAfter time when an autosurgeon is used on yourself.
    /// Shorter than <see cref="DefaultInstallTime"/> since you only need to hold still.</summary>
    public static readonly TimeSpan DefaultSelfInstallTime = TimeSpan.FromSeconds(3);
}

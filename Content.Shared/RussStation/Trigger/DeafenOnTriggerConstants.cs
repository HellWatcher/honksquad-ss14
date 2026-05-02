namespace Content.Shared.RussStation.Trigger;

/// <summary>
/// Tunable defaults for <see cref="DeafenOnTriggerComponent"/>. Per the magic-number audit,
/// every numeric default referenced from a [DataField] in this folder lives here.
/// </summary>
public static class DeafenOnTriggerConstants
{
    /// <summary>Default radius in tiles for the deafen area.</summary>
    public const float DefaultRange = 5f;

    /// <summary>Default duration of the deafness applied to each victim.</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(8);

    /// <summary>Default per-entity deafening probability.</summary>
    public const float DefaultProbability = 1f;
}

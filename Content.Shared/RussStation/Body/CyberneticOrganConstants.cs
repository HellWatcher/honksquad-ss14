namespace Content.Shared.RussStation.Body;

/// <summary>
/// Tunable defaults for cybernetic organ components. Per the magic-number audit, every numeric
/// default referenced from a [DataField] in this folder lives here so reviewers can re-tune
/// values centrally without grepping through every component file.
/// </summary>
public static class CyberneticOrganConstants
{
    /// <summary>Default EMP vulnerability multiplier on <see cref="CyberneticOrganComponent"/>.</summary>
    public const float DefaultEmpVulnerability = 1.0f;

    /// <summary>Cooldown between auto-injections for <see cref="CyberneticHeartComponent"/>.</summary>
    public static readonly TimeSpan HeartEpinephrineCooldown = TimeSpan.FromSeconds(120);

    /// <summary>Default hunger-decay multiplier on <see cref="CyberneticStomachComponent"/>.</summary>
    public const float DefaultStomachDecayMultiplier = 0.5f;

    /// <summary>Default overdose-threshold multiplier on the cybernetic liver and the
    /// derived <see cref="OverdoseResistanceComponent"/> applied to its host body.</summary>
    public const float DefaultOverdoseThresholdMultiplier = 1.5f;
}

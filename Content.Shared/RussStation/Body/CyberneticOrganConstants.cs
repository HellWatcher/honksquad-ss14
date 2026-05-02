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
}

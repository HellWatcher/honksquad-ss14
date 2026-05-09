// HONK - Issue #302 follow-up: research-server points for bluespace crystals.
// Mirrors SS13's points = 50 per crystal on /obj/item/stack/ore/bluespace_crystal.
// Stack-aware: each AfterInteract on a research server consumes one from the stack
// and credits the server with Points. Upstream's ResearchDiskComponent QueueDels
// the whole entity on use, so a stack-aware variant is needed for the unified
// bluespace crystal entity.

namespace Content.Server.RussStation.Bluespace.Components;

[RegisterComponent]
public sealed partial class BluespaceResearchInsertComponent : Component
{
    /// <summary>
    /// Research points credited per crystal consumed. SS13's
    /// <c>points = 50</c> on <c>/obj/item/stack/ore/bluespace_crystal</c>.
    /// </summary>
    [DataField]
    public int Points = BluespaceResearchInsertConstants.PointsPerCrystal;
}

internal static class BluespaceResearchInsertConstants
{
    /// <summary>
    /// Default research points per bluespace crystal, matches SS13.
    /// </summary>
    public const int PointsPerCrystal = 50;
}

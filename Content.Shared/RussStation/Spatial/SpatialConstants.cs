namespace Content.Shared.RussStation.Spatial;

public static class SpatialConstants
{
    public const float TileCenterOffset = 0.5f;

    /// <summary>
    /// Inclusive-to-exclusive extent adjust for tile counts:
    /// a span from <c>min</c> to <c>max</c> covers <c>max - min + 1</c> tiles.
    /// </summary>
    public const int InclusiveExtentAdjust = 1;

    /// <summary>
    /// Floor for the shorter dimension used when computing an aspect ratio,
    /// so a one-tile-wide strip never divides by zero.
    /// </summary>
    public const int MinAspectDivisorFloor = 1;

    /// <summary>
    /// Bisection divisor for subdivision: the bounding-box is split at its midpoint.
    /// </summary>
    public const int SubdivisionHalfDivisor = 2;
}

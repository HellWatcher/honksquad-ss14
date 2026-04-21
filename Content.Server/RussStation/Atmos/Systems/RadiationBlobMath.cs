using Content.Server.RussStation.Atmos;
using Content.Shared.RussStation.Spatial;
using Robust.Shared.Maths;

namespace Content.Server.RussStation.Atmos.Systems;

/// <summary>
///     Radiation-specific math built on top of <see cref="TileGridMath"/>.
///     Centroid, gradient calculation, and blob subdivision for
///     <see cref="AtmosRadiationPulseSystem"/>.
/// </summary>
public static class RadiationBlobMath
{
    private const float MinRadiusForSlope = 0.5f;
    private const float DefaultSlope = 0.5f;

    /// <summary>
    ///     Compute the intensity-weighted centroid of a set of tiles.
    /// </summary>
    public static (float Cx, float Cy) Centroid(List<(Vector2i Tile, float Rads)> tiles, float totalRads)
    {
        Span<Vector2i> positions = stackalloc Vector2i[tiles.Count];
        Span<float> weights = stackalloc float[tiles.Count];
        for (var i = 0; i < tiles.Count; i++)
        {
            positions[i] = tiles[i].Tile;
            weights[i] = tiles[i].Rads;
        }

        var c = TileGridMath.WeightedCentroid(positions, weights, totalRads);
        return (c.X, c.Y);
    }

    /// <summary>
    ///     Compute source intensity and slope for a chunk of tiles.
    ///     Intensity is the peak tile rads; slope is derived from the
    ///     min/max ratio so intensity falls to min at the chunk edge.
    /// </summary>
    public static (float Intensity, float Slope) GradientParams(List<(Vector2i Tile, float Rads)> tiles)
    {
        var minRads = float.MaxValue;
        var maxRads = float.MinValue;
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        foreach (var (tile, rads) in tiles)
        {
            if (rads < minRads) minRads = rads;
            if (rads > maxRads) maxRads = rads;
            if (tile.X < minX) minX = tile.X;
            if (tile.Y < minY) minY = tile.Y;
            if (tile.X > maxX) maxX = tile.X;
            if (tile.Y > maxY) maxY = tile.Y;
        }

        var radius = Math.Max(
            maxX - minX + AtmosConstants.RadiationBlobInclusiveExtentAdjust,
            maxY - minY + AtmosConstants.RadiationBlobInclusiveExtentAdjust)
            / AtmosConstants.RadiationBlobRadiusDivisor;
        var slope = radius > MinRadiusForSlope && minRads > 0f
            ? (maxRads / minRads - AtmosConstants.RadiationBlobFlatGradientBaseline) / radius
            : DefaultSlope;

        return (maxRads, slope);
    }

    /// <summary>
    ///     Recursively subdivide a blob into roughly-square chunks.
    ///     Returns a list of tile groups, each suitable for spawning a single source.
    /// </summary>
    public static List<List<(Vector2i Tile, float Rads)>> Subdivide(List<(Vector2i Tile, float Rads)> tiles)
    {
        return TileGridMath.Subdivide(tiles, static entry => entry.Tile);
    }
}

using Robust.Shared.Maths;

namespace Content.Shared.RussStation.Spatial;

/// <summary>
///     General-purpose spatial math for discrete tile grids.
///     Weighted centroid, bounding-box computation, and recursive subdivision.
/// </summary>
public static class TileGridMath
{
    /// <summary>
    ///     Default aspect ratio threshold for subdivision.
    ///     A tile group with max(w,h)/min(w,h) above this gets split.
    /// </summary>
    public const float DefaultAspectThreshold = 1.5f;

    /// <summary>
    ///     Default minimum tile count below which subdivision never occurs.
    /// </summary>
    public const int DefaultMinSplitSize = 4;

    /// <summary>
    ///     Compute the weighted centroid of a set of tiles.
    ///     Each tile center is at (X + 0.5, Y + 0.5) in world coordinates.
    /// </summary>
    /// <param name="tiles">Tile positions.</param>
    /// <param name="weights">Per-tile weights (same length as tiles).</param>
    /// <param name="totalWeight">Sum of all weights. Must be > 0.</param>
    public static (float X, float Y) WeightedCentroid(
        ReadOnlySpan<Vector2i> tiles,
        ReadOnlySpan<float> weights,
        float totalWeight)
    {
        var cx = 0f;
        var cy = 0f;
        for (var i = 0; i < tiles.Length; i++)
        {
            cx += (tiles[i].X + 0.5f) * weights[i];
            cy += (tiles[i].Y + 0.5f) * weights[i];
        }
        return (cx / totalWeight, cy / totalWeight);
    }

    /// <summary>
    ///     Compute the axis-aligned bounding box of a set of tiles.
    /// </summary>
    public static Box2i BoundingBox(ReadOnlySpan<Vector2i> tiles)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        for (var i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t.X < minX) minX = t.X;
            if (t.Y < minY) minY = t.Y;
            if (t.X > maxX) maxX = t.X;
            if (t.Y > maxY) maxY = t.Y;
        }

        return new Box2i(minX, minY, maxX, maxY);
    }

    /// <summary>
    ///     Recursively subdivide a set of tiles into roughly-square groups
    ///     by splitting along the longer bounding-box axis at the midpoint.
    /// </summary>
    /// <typeparam name="T">Arbitrary payload carried with each tile.</typeparam>
    /// <param name="tiles">Tiles to subdivide.</param>
    /// <param name="tileSelector">Extracts the grid position from each element.</param>
    /// <param name="aspectThreshold">Max aspect ratio before splitting.</param>
    /// <param name="minSplitSize">Groups at or below this size are never split.</param>
    public static List<List<T>> Subdivide<T>(
        List<T> tiles,
        Func<T, Vector2i> tileSelector,
        float aspectThreshold = DefaultAspectThreshold,
        int minSplitSize = DefaultMinSplitSize)
    {
        var results = new List<List<T>>();
        SubdivideInto(tiles, tileSelector, aspectThreshold, minSplitSize, results);
        return results;
    }

    private static void SubdivideInto<T>(
        List<T> tiles,
        Func<T, Vector2i> tileSelector,
        float aspectThreshold,
        int minSplitSize,
        List<List<T>> results)
    {
        if (tiles.Count == 0)
            return;

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        foreach (var entry in tiles)
        {
            var pos = tileSelector(entry);
            if (pos.X < minX) minX = pos.X;
            if (pos.Y < minY) minY = pos.Y;
            if (pos.X > maxX) maxX = pos.X;
            if (pos.Y > maxY) maxY = pos.Y;
        }

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var longer = Math.Max(width, height);
        var shorter = Math.Max(1, Math.Min(width, height));

        if (tiles.Count <= minSplitSize || (float) longer / shorter <= aspectThreshold)
        {
            results.Add(tiles);
            return;
        }

        var halfA = new List<T>();
        var halfB = new List<T>();

        if (width >= height)
        {
            var midX = minX + width / 2;
            foreach (var entry in tiles)
            {
                if (tileSelector(entry).X <= midX)
                    halfA.Add(entry);
                else
                    halfB.Add(entry);
            }
        }
        else
        {
            var midY = minY + height / 2;
            foreach (var entry in tiles)
            {
                if (tileSelector(entry).Y <= midY)
                    halfA.Add(entry);
                else
                    halfB.Add(entry);
            }
        }

        SubdivideInto(halfA, tileSelector, aspectThreshold, minSplitSize, results);
        SubdivideInto(halfB, tileSelector, aspectThreshold, minSplitSize, results);
    }
}

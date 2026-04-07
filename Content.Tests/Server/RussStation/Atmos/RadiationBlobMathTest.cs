using System.Collections.Generic;
using System.Linq;
using Content.Server.RussStation.Atmos.Systems;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Server.RussStation.Atmos;

[TestFixture, TestOf(typeof(RadiationBlobMath))]
[Parallelizable(ParallelScope.All)]
public sealed class RadiationBlobMathTest
{
    [Test]
    public void Centroid_SingleTile_ReturnsTileCenter()
    {
        var tiles = new List<(Vector2i Tile, float Rads)>
        {
            (new Vector2i(3, 5), 2.0f),
        };

        var (cx, cy) = RadiationBlobMath.Centroid(tiles, 2.0f);

        Assert.That(cx, Is.EqualTo(3.5f).Within(0.001f));
        Assert.That(cy, Is.EqualTo(5.5f).Within(0.001f));
    }

    [Test]
    public void Centroid_WeightedTowardHotTile()
    {
        var tiles = new List<(Vector2i Tile, float Rads)>
        {
            (new Vector2i(0, 0), 1.0f),
            (new Vector2i(4, 0), 3.0f),
        };

        var (cx, _) = RadiationBlobMath.Centroid(tiles, 4.0f);

        // Weighted: (0.5*1 + 4.5*3) / 4 = 14/4 = 3.5
        Assert.That(cx, Is.EqualTo(3.5f).Within(0.001f));
    }

    [Test]
    public void GradientParams_SingleTile_DefaultSlope()
    {
        var tiles = new List<(Vector2i Tile, float Rads)>
        {
            (new Vector2i(0, 0), 3.0f),
        };

        var (intensity, slope) = RadiationBlobMath.GradientParams(tiles);

        Assert.That(intensity, Is.EqualTo(3.0f));
        Assert.That(slope, Is.EqualTo(0.5f));
    }

    [Test]
    public void GradientParams_UniformTiles_LowSlope()
    {
        var tiles = new List<(Vector2i Tile, float Rads)>
        {
            (new Vector2i(0, 0), 2.0f),
            (new Vector2i(1, 0), 2.0f),
            (new Vector2i(2, 0), 2.0f),
        };

        var (intensity, slope) = RadiationBlobMath.GradientParams(tiles);

        Assert.That(intensity, Is.EqualTo(2.0f));
        // max/min = 1, so (1 - 1) / radius = 0
        Assert.That(slope, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void GradientParams_HotCenter_SteeperSlope()
    {
        var tiles = new List<(Vector2i Tile, float Rads)>
        {
            (new Vector2i(0, 0), 1.0f),
            (new Vector2i(1, 0), 4.0f),
            (new Vector2i(2, 0), 1.0f),
        };

        var (intensity, slope) = RadiationBlobMath.GradientParams(tiles);

        Assert.That(intensity, Is.EqualTo(4.0f));
        // radius = 3/2 = 1.5, (4/1 - 1) / 1.5 = 2.0
        Assert.That(slope, Is.EqualTo(2.0f).Within(0.001f));
    }

    [Test]
    public void GradientParams_AtEdge_IntensityFallsToMin()
    {
        var tiles = new List<(Vector2i Tile, float Rads)>
        {
            (new Vector2i(0, 0), 1.0f),
            (new Vector2i(1, 0), 4.0f),
            (new Vector2i(2, 0), 1.0f),
        };

        var (intensity, slope) = RadiationBlobMath.GradientParams(tiles);
        var radius = 1.5f;

        // received = intensity / (slope * distance + 1)
        var atEdge = intensity / (slope * radius + 1f);

        Assert.That(atEdge, Is.EqualTo(1.0f).Within(0.001f));
    }

    [Test]
    public void Subdivide_SmallBlob_NoSplit()
    {
        var tiles = new List<(Vector2i Tile, float Rads)>
        {
            (new Vector2i(0, 0), 1.0f),
            (new Vector2i(1, 0), 1.0f),
            (new Vector2i(0, 1), 1.0f),
            (new Vector2i(1, 1), 1.0f),
        };

        var chunks = RadiationBlobMath.Subdivide(tiles);

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0], Has.Count.EqualTo(4));
    }

    [Test]
    public void Subdivide_SquareBlob_NoSplit()
    {
        // 3x3 square, 9 tiles, aspect ratio 1.0
        var tiles = new List<(Vector2i Tile, float Rads)>();
        for (var x = 0; x < 3; x++)
        for (var y = 0; y < 3; y++)
            tiles.Add((new Vector2i(x, y), 1.0f));

        var chunks = RadiationBlobMath.Subdivide(tiles);

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0], Has.Count.EqualTo(9));
    }

    [Test]
    public void Subdivide_LongBlob_Splits()
    {
        // 6x1, aspect ratio 6.0, well above threshold
        var tiles = new List<(Vector2i Tile, float Rads)>();
        for (var x = 0; x < 6; x++)
            tiles.Add((new Vector2i(x, 0), 1.0f));

        var chunks = RadiationBlobMath.Subdivide(tiles);

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));

        var totalTiles = chunks.Sum(c => c.Count);
        Assert.That(totalTiles, Is.EqualTo(6));
    }

    [Test]
    public void Subdivide_LShapedBlob_SplitsAlongLongerAxis()
    {
        // L-shape: 5 wide, 3 tall
        var tiles = new List<(Vector2i Tile, float Rads)>
        {
            (new Vector2i(0, 0), 1.0f),
            (new Vector2i(1, 0), 1.0f),
            (new Vector2i(2, 0), 1.0f),
            (new Vector2i(3, 0), 1.0f),
            (new Vector2i(4, 0), 1.0f),
            (new Vector2i(0, 1), 1.0f),
            (new Vector2i(0, 2), 1.0f),
        };

        var chunks = RadiationBlobMath.Subdivide(tiles);

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));

        var totalTiles = chunks.Sum(c => c.Count);
        Assert.That(totalTiles, Is.EqualTo(7));
    }

    [Test]
    public void Subdivide_PreservesRadsValues()
    {
        var tiles = new List<(Vector2i Tile, float Rads)>();
        for (var x = 0; x < 8; x++)
            tiles.Add((new Vector2i(x, 0), x + 1.0f));

        var chunks = RadiationBlobMath.Subdivide(tiles);
        var totalRads = chunks.SelectMany(c => c).Sum(t => t.Rads);

        // 1+2+3+4+5+6+7+8 = 36
        Assert.That(totalRads, Is.EqualTo(36.0f).Within(0.001f));
    }

    [Test]
    public void Subdivide_Empty_ReturnsEmpty()
    {
        var tiles = new List<(Vector2i Tile, float Rads)>();

        var chunks = RadiationBlobMath.Subdivide(tiles);

        Assert.That(chunks, Is.Empty);
    }
}

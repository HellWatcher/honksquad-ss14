using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.RussStation.Spatial;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.RussStation.Spatial;

[TestFixture, TestOf(typeof(TileGridMath))]
[Parallelizable(ParallelScope.All)]
public sealed class TileGridMathTest
{
    [Test]
    public void WeightedCentroid_SingleTile_ReturnsTileCenter()
    {
        Span<Vector2i> tiles = stackalloc Vector2i[] { new(3, 5) };
        Span<float> weights = stackalloc float[] { 1.0f };

        var (cx, cy) = TileGridMath.WeightedCentroid(tiles, weights, 1.0f);

        Assert.That(cx, Is.EqualTo(3.5f).Within(0.001f));
        Assert.That(cy, Is.EqualTo(5.5f).Within(0.001f));
    }

    [Test]
    public void WeightedCentroid_UniformWeights_ReturnsGeometricCenter()
    {
        Span<Vector2i> tiles = stackalloc Vector2i[]
        {
            new(0, 0), new(2, 0),
        };
        Span<float> weights = stackalloc float[] { 1.0f, 1.0f };

        var (cx, _) = TileGridMath.WeightedCentroid(tiles, weights, 2.0f);

        // (0.5 + 2.5) / 2 = 1.5
        Assert.That(cx, Is.EqualTo(1.5f).Within(0.001f));
    }

    [Test]
    public void WeightedCentroid_HeavyWeight_PullsToward()
    {
        Span<Vector2i> tiles = stackalloc Vector2i[]
        {
            new(0, 0), new(4, 0),
        };
        Span<float> weights = stackalloc float[] { 1.0f, 3.0f };

        var (cx, _) = TileGridMath.WeightedCentroid(tiles, weights, 4.0f);

        // (0.5*1 + 4.5*3) / 4 = 14/4 = 3.5
        Assert.That(cx, Is.EqualTo(3.5f).Within(0.001f));
    }

    [Test]
    public void BoundingBox_SingleTile()
    {
        Span<Vector2i> tiles = stackalloc Vector2i[] { new(3, 5) };

        var box = TileGridMath.BoundingBox(tiles);

        Assert.That(box.Left, Is.EqualTo(3));
        Assert.That(box.Bottom, Is.EqualTo(5));
        Assert.That(box.Right, Is.EqualTo(3));
        Assert.That(box.Top, Is.EqualTo(5));
    }

    [Test]
    public void BoundingBox_MultipleTiles()
    {
        Span<Vector2i> tiles = stackalloc Vector2i[]
        {
            new(1, 2), new(5, 8), new(3, 0),
        };

        var box = TileGridMath.BoundingBox(tiles);

        Assert.That(box.Left, Is.EqualTo(1));
        Assert.That(box.Bottom, Is.EqualTo(0));
        Assert.That(box.Right, Is.EqualTo(5));
        Assert.That(box.Top, Is.EqualTo(8));
    }

    [Test]
    public void Subdivide_SmallGroup_NoSplit()
    {
        var tiles = new List<Vector2i>
        {
            new(0, 0), new(1, 0), new(0, 1), new(1, 1),
        };

        var chunks = TileGridMath.Subdivide(tiles, static t => t);

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0], Has.Count.EqualTo(4));
    }

    [Test]
    public void Subdivide_SquareGroup_NoSplit()
    {
        var tiles = new List<Vector2i>();
        for (var x = 0; x < 3; x++)
        for (var y = 0; y < 3; y++)
            tiles.Add(new Vector2i(x, y));

        var chunks = TileGridMath.Subdivide(tiles, static t => t);

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0], Has.Count.EqualTo(9));
    }

    [Test]
    public void Subdivide_LongGroup_Splits()
    {
        var tiles = new List<Vector2i>();
        for (var x = 0; x < 6; x++)
            tiles.Add(new Vector2i(x, 0));

        var chunks = TileGridMath.Subdivide(tiles, static t => t);

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(chunks.Sum(c => c.Count), Is.EqualTo(6));
    }

    [Test]
    public void Subdivide_Empty_ReturnsEmpty()
    {
        var tiles = new List<Vector2i>();

        var chunks = TileGridMath.Subdivide(tiles, static t => t);

        Assert.That(chunks, Is.Empty);
    }

    [Test]
    public void Subdivide_WithPayload_PreservesData()
    {
        var tiles = new List<(Vector2i Pos, string Name)>();
        for (var x = 0; x < 8; x++)
            tiles.Add((new Vector2i(x, 0), $"tile_{x}"));

        var chunks = TileGridMath.Subdivide(tiles, static t => t.Pos);
        var allNames = chunks.SelectMany(c => c).Select(t => t.Name).OrderBy(n => n).ToList();

        Assert.That(allNames, Has.Count.EqualTo(8));
        for (var i = 0; i < 8; i++)
            Assert.That(allNames[i], Is.EqualTo($"tile_{i}"));
    }

    [Test]
    public void Subdivide_CustomThreshold_Respected()
    {
        // 4x1 strip: aspect 4.0, above default 1.5 but below custom 5.0
        var tiles = new List<Vector2i>();
        for (var x = 0; x < 4; x++)
            tiles.Add(new Vector2i(x, 0));

        var chunks = TileGridMath.Subdivide(tiles, static t => t, aspectThreshold: 5.0f);

        Assert.That(chunks, Has.Count.EqualTo(1));
    }
}

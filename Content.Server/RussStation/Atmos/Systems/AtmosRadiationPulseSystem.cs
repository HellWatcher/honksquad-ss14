using Content.Shared.Radiation.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Spawners;

namespace Content.Server.RussStation.Atmos.Systems;

/// <summary>
///     Collects <see cref="AtmosRadiationPulseEvent"/>s raised by gas reactions and spawns
///     short-lived radiation source entities so the existing gridcast system handles
///     wall occlusion, resistance, etc.
/// </summary>
/// <remarks>
///     Pulses on the same tile within one tick are flood-filled into connected blobs.
///     Non-square blobs are recursively subdivided along their longer axis so the
///     radiation field follows the shape of the reacting gas cloud.
///     Each source lives for one radiation gridcast cycle (~1 s) then despawns.
/// </remarks>
public sealed class AtmosRadiationPulseSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;

    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), float> _pending = new();

    private const float PulseLifetime = 1.5f;
    private const float MinIntensity = 0.5f;

    /// <summary>
    ///     Aspect ratio threshold. A blob with max(w,h)/min(w,h) above this gets split.
    /// </summary>
    private const float AspectThreshold = 1.5f;

    /// <summary>
    ///     Blobs at or below this tile count are never subdivided.
    /// </summary>
    private const int MinSplitSize = 4;

    // Reusable collections to avoid per-tick allocation.
    private readonly HashSet<(EntityUid, Vector2i)> _visited = new();
    private readonly Queue<(EntityUid, Vector2i)> _queue = new();
    private readonly List<(Vector2i Tile, float Rads)> _blob = new();
    private readonly List<(Vector2i Tile, float Rads)> _splitA = new();
    private readonly List<(Vector2i Tile, float Rads)> _splitB = new();

    private static readonly Vector2i[] Neighbors =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AtmosRadiationPulseEvent>(OnPulse);
    }

    private void OnPulse(ref AtmosRadiationPulseEvent ev)
    {
        var key = (ev.GridUid, ev.Tile);
        if (_pending.TryGetValue(key, out var existing))
            _pending[key] = existing + ev.Rads;
        else
            _pending[key] = ev.Rads;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        _visited.Clear();

        foreach (var (key, rads) in _pending)
        {
            if (_visited.Contains(key))
                continue;

            if (rads < MinIntensity)
            {
                _visited.Add(key);
                continue;
            }

            FloodFill(key.Grid, key.Tile);

            if (_blob.Count == 0)
                continue;

            if (!TryComp<MapGridComponent>(key.Grid, out var grid))
                continue;

            SpawnSubdivided(key.Grid, grid, _blob);
        }

        _pending.Clear();
    }

    /// <summary>
    ///     Recursively subdivides a blob along its longer bounding-box axis
    ///     until each chunk is roughly square, then spawns a source per chunk.
    /// </summary>
    private void SpawnSubdivided(
        EntityUid gridUid,
        MapGridComponent grid,
        List<(Vector2i Tile, float Rads)> tiles)
    {
        if (tiles.Count == 0)
            return;

        // Compute bounding box.
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        foreach (var (tile, _) in tiles)
        {
            if (tile.X < minX) minX = tile.X;
            if (tile.Y < minY) minY = tile.Y;
            if (tile.X > maxX) maxX = tile.X;
            if (tile.Y > maxY) maxY = tile.Y;
        }

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var longer = Math.Max(width, height);
        var shorter = Math.Max(1, Math.Min(width, height));

        // If roughly square or too small to split, spawn a single source.
        if (tiles.Count <= MinSplitSize || (float) longer / shorter <= AspectThreshold)
        {
            SpawnSource(gridUid, grid, tiles);
            return;
        }

        // Split along the longer axis at the midpoint.
        _splitA.Clear();
        _splitB.Clear();

        if (width >= height)
        {
            var midX = minX + width / 2;
            foreach (var entry in tiles)
            {
                if (entry.Tile.X <= midX)
                    _splitA.Add(entry);
                else
                    _splitB.Add(entry);
            }
        }
        else
        {
            var midY = minY + height / 2;
            foreach (var entry in tiles)
            {
                if (entry.Tile.Y <= midY)
                    _splitA.Add(entry);
                else
                    _splitB.Add(entry);
            }
        }

        // Copy to avoid aliasing since recursion reuses _splitA/_splitB.
        var halfA = new List<(Vector2i, float)>(_splitA);
        var halfB = new List<(Vector2i, float)>(_splitB);

        SpawnSubdivided(gridUid, grid, halfA);
        SpawnSubdivided(gridUid, grid, halfB);
    }

    private void SpawnSource(
        EntityUid gridUid,
        MapGridComponent grid,
        List<(Vector2i Tile, float Rads)> tiles)
    {
        var cx = 0f;
        var cy = 0f;
        var totalRads = 0f;
        foreach (var (tile, rads) in tiles)
        {
            cx += (tile.X + 0.5f) * rads;
            cy += (tile.Y + 0.5f) * rads;
            totalRads += rads;
        }

        if (totalRads < MinIntensity)
            return;

        cx /= totalRads;
        cy /= totalRads;

        var centroidTile = new Vector2i((int) Math.Floor(cx), (int) Math.Floor(cy));
        var coords = _map.GridTileToLocal(gridUid, grid, centroidTile);

        var uid = Spawn(null, coords);

        var source = EnsureComp<RadiationSourceComponent>(uid);
        source.Intensity = totalRads;
        source.Enabled = true;

        var despawn = EnsureComp<TimedDespawnComponent>(uid);
        despawn.Lifetime = PulseLifetime;
    }

    /// <summary>
    ///     BFS flood-fill from a seed tile, collecting all connected pending tiles into <see cref="_blob"/>.
    /// </summary>
    private void FloodFill(EntityUid gridUid, Vector2i seed)
    {
        _blob.Clear();
        _queue.Clear();

        var start = (gridUid, seed);
        _queue.Enqueue(start);
        _visited.Add(start);

        while (_queue.Count > 0)
        {
            var (grid, tile) = _queue.Dequeue();
            var key = (grid, tile);

            if (!_pending.TryGetValue(key, out var rads))
                continue;

            if (rads < MinIntensity)
                continue;

            _blob.Add((tile, rads));

            foreach (var offset in Neighbors)
            {
                var neighbor = (grid, tile + offset);
                if (_visited.Add(neighbor) && _pending.ContainsKey(neighbor))
                    _queue.Enqueue(neighbor);
            }
        }
    }
}

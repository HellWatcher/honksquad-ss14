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


    // Reusable collections to avoid per-tick allocation.
    private readonly HashSet<(EntityUid, Vector2i)> _visited = new();
    private readonly Queue<(EntityUid, Vector2i)> _queue = new();
    private readonly List<(Vector2i Tile, float Rads)> _blob = new();

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

            var chunks = RadiationBlobMath.Subdivide(_blob);
            foreach (var chunk in chunks)
                SpawnSource(key.Grid, grid, chunk);
        }

        _pending.Clear();
    }

    private void SpawnSource(
        EntityUid gridUid,
        MapGridComponent grid,
        List<(Vector2i Tile, float Rads)> tiles)
    {
        var totalRads = 0f;
        foreach (var (_, rads) in tiles)
            totalRads += rads;

        if (totalRads < MinIntensity)
            return;

        var (cx, cy) = RadiationBlobMath.Centroid(tiles, totalRads);
        var centroidTile = new Vector2i((int) Math.Floor(cx), (int) Math.Floor(cy));
        var coords = _map.GridTileToLocal(gridUid, grid, centroidTile);

        var uid = Spawn(null, coords);

        var (intensity, slope) = RadiationBlobMath.GradientParams(tiles);

        var source = EnsureComp<RadiationSourceComponent>(uid);
        source.Intensity = intensity;
        source.Slope = slope;
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

using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Mono.Cleanup;

/// <summary>
///     System with helper methods for entity cleanup.
/// </summary>
public sealed partial class CleanupHelperSystem : EntitySystem
{
    [Dependency] private IMapManager _mapMan = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private List<Entity<MapGridComponent>> _gridsFound = new();
    private HashSet<Entity<MindContainerComponent>> _mindsFound = new();

    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<MindComponent> _mindQuery;

    // Forge-Change: cached snapshot of player world positions + grid, refreshed at most once per
    // PlayerCacheTtl. Cleanup loops can call HasNearbyPlayers thousands of times per scan;
    // a short-lived cache eliminates the per-call spatial lookup without affecting correctness
    // (players move slowly relative to cleanup radii ~ 628 units).
    private readonly List<(MapId Map, Vector2 Pos, EntityUid? Grid)> _playerCache = new();
    private TimeSpan _playerCacheUntil = TimeSpan.MinValue;
    private static readonly TimeSpan PlayerCacheTtl = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        _ghostQuery = GetEntityQuery<GhostComponent>();
        _mindQuery = GetEntityQuery<MindComponent>();
    }

    /// <summary>
    ///     Refreshes the player position cache if it has expired. Cheap when warm.
    /// </summary>
    private void EnsurePlayerCache()
    {
        if (_timing.CurTime < _playerCacheUntil)
            return;

        _playerCache.Clear();
        var query = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mind, out var xform))
        {
            if (!mind.HasMind || _ghostQuery.HasComp(uid))
                continue;
            if (_mindQuery.CompOrNull(mind.Mind!.Value)?.OwnedEntity == null)
                continue;
            if (xform.MapID == MapId.Nullspace)
                continue;

            _playerCache.Add((xform.MapID, _transform.GetWorldPosition(xform), xform.GridUid)); // Forge-Change: also cache GridUid
        }
        _playerCacheUntil = _timing.CurTime + PlayerCacheTtl;
    }

    /// <summary>
    ///     Whether there is an entity with a player bound to it in radius. Counts dead people and brains but not ghosts.
    /// </summary>
    public bool HasNearbyPlayers(EntityCoordinates coord, float radius)
    {
        EnsurePlayerCache();
        if (_playerCache.Count == 0)
            return false;

        var mapPos = _transform.ToMapCoordinates(coord);
        if (mapPos.MapId == MapId.Nullspace)
            return false;

        var radSq = radius * radius;
        foreach (var (map, pos, _) in _playerCache)
        {
            if (map != mapPos.MapId)
                continue;
            if (Vector2.DistanceSquared(mapPos.Position, pos) <= radSq)
                return true;
        }
        return false;
    }

    /// <summary>
    ///     Grids that currently have a mind-bound non-ghost player on them.
    /// </summary>
    // Forge-Change-Start: occupied-grid set for shuttle idle cleanup.
    public void CollectOccupiedGrids(HashSet<EntityUid> into)
    {
        EnsurePlayerCache();
        foreach (var (_, _, grid) in _playerCache)
        {
            if (grid is { } gridUid)
                into.Add(gridUid);
        }
    }
    // Forge-Change-End

    /// <summary>
    ///     Whether there is a grid in radius. Approximate.
    /// </summary>
    public bool HasNearbyGrids(EntityCoordinates coord, float radius)
    {
        var rangeVec = new Vector2(radius, radius);
        var mapPos = _transform.ToMapCoordinates(coord);
        var pos = mapPos.Position;

        _gridsFound.Clear();
        _mapMan.FindGridsIntersecting(mapPos.MapId, new Box2(pos - rangeVec, pos + rangeVec), ref _gridsFound, true);

        return _gridsFound.Count > 0;
    }
}

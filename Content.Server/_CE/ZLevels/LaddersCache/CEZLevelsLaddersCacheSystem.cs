using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.LaddersCache;

public sealed partial class CEZLevelsLaddersCacheSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<CEZLevelsLaddersCacheComponent> _cacheQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelHighGroundComponent, MapInitEvent>(OnLadderInit);
        SubscribeLocalEvent<CEZLevelHighGroundComponent, ComponentShutdown>(OnLadderShutdown);
        SubscribeLocalEvent<CEZLevelHighGroundComponent, AnchorStateChangedEvent>(OnLadderAnchorChanged);
    }

    private void OnLadderInit(Entity<CEZLevelHighGroundComponent> ent, ref MapInitEvent args)
    {
        TryCacheLadder(ent);
    }

    private void OnLadderShutdown(Entity<CEZLevelHighGroundComponent> ent, ref ComponentShutdown args)
    {
        TryRemoveCache(ent);
    }

    private void OnLadderAnchorChanged(Entity<CEZLevelHighGroundComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            TryCacheLadder(ent);
        else
            TryRemoveCache(ent);
    }


    /// <summary>
    /// Checks if the HighGround entity qualifies as a navigable slope (ramp),
    /// and if so, registers it in the cache on its parent grid.
    /// </summary>
    private void TryCacheLadder(Entity<CEZLevelHighGroundComponent> ent)
    {
        if (ent.Comp.HeightCurve.Count < 2)
            return;

        if (!_xformQuery.TryGetComponent(ent, out var xform))
            return;

        if (!xform.Anchored)
            return;

        var gridUid = xform.GridUid;
        if (gridUid == null || !_gridQuery.TryGetComponent(gridUid.Value, out var grid))
            return;

        var cache = EnsureComp<CEZLevelsLaddersCacheComponent>(gridUid.Value);
        var tilePos = _map.WorldToTile(gridUid.Value, grid, _transform.GetWorldPosition(xform));
        var dir = xform.LocalRotation.GetCardinalDir();

        cache.Slopes[tilePos] = new CECachedSlope
        {
            Entity = ent,
            Direction = dir,
        };
    }


    /// <summary>
    /// Removes a slope entity from the cache when it's destroyed, unanchored, or removed.
    /// </summary>
    private void TryRemoveCache(Entity<CEZLevelHighGroundComponent> ent)
    {
        if (!_xformQuery.TryGetComponent(ent, out var xform))
            return;

        var gridUid = xform.GridUid;
        if (gridUid == null || !_cacheQuery.TryGetComponent(gridUid.Value, out var cache))
            return;

        if (!_gridQuery.TryGetComponent(gridUid.Value, out var grid))
            return;

        var tilePos = _map.WorldToTile(gridUid.Value, grid, _transform.GetWorldPosition(xform));
        cache.Slopes.Remove(tilePos);
    }

    /// <summary>
    /// Finds the nearest cached ladder on a given grid to the specified world position.
    /// </summary>
    /// <returns>True if a slope was found.</returns>
    [PublicAPI]
    public bool GetNearestLadder(
        EntityUid gridUid,
        Vector2 originPos,
        float maxRange,
        out Vector2i slopeTilePos,
        out CECachedSlope slope)
    {
        slopeTilePos = default;
        slope = default;

        if (!_cacheQuery.TryGetComponent(gridUid, out var cache))
            return false;

        if (!_gridQuery.TryGetComponent(gridUid, out var grid))
            return false;

        var originTile = _map.WorldToTile(gridUid, grid, originPos);
        var maxRangeInt = (int)Math.Ceiling(maxRange);
        var bestDistSq = float.MaxValue;
        var found = false;

        foreach (var (tilePos, cachedSlope) in cache.Slopes)
        {
            // Quick Manhattan pre-filter
            var dx = Math.Abs(tilePos.X - originTile.X);
            var dy = Math.Abs(tilePos.Y - originTile.Y);
            if (dx > maxRangeInt || dy > maxRangeInt)
                continue;

            var distSq = (tilePos.X - originTile.X) * (tilePos.X - originTile.X)
                         + (tilePos.Y - originTile.Y) * (tilePos.Y - originTile.Y);

            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            slopeTilePos = tilePos;
            slope = cachedSlope;
            found = true;
        }

        return found;
    }
}

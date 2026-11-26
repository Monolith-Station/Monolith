/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Chasm;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    private readonly List<EntityUid> _dirtyMovementBodies = new();

    private void InitializeMovement()
    {
        SubscribeLocalEvent<CEZPhysicsComponent, CEZLevelMapMoveEvent>(OnZLevelMapMove);
        SubscribeLocalEvent<CEZPhysicsComponent, MoveEvent>(OnMoveEvent);
        SubscribeLocalEvent<CEZMapComponent, TileChangedEvent>(OnTileChanged);
    }

    [PublicAPI]
    public float DistanceToGround(Entity<CEZPhysicsComponent?> target)
    {
        if (!Resolve(target, ref target.Comp, false))
            return 0;

        return target.Comp.LocalPosition - target.Comp.CachedGroundHeight;
    }

    /// <summary>Altitude above the current floor in levels, including transit and grid motion.</summary>
    [PublicAPI]
    public float GetLocalAltitude(EntityUid uid)
    {
        var xform = Transform(uid);

        if (TryComp<CEZTransitMapComponent>(xform.MapUid, out var transit))
            return GetTransitProgress(transit) * GetTransitSpan(transit);

        if (ZPhysicsQuery.TryComp(uid, out var zPhys) && zPhys.LocalPosition > 0f)
            return Math.Clamp(zPhys.LocalPosition, 0f, 1f);

        // Standing on a grid that is itself climbing within the level.
        if (xform.GridUid is { } grid && grid != uid && ZPhysicsQuery.TryComp(grid, out var gridPhys))
            return Math.Clamp(gridPhys.LocalPosition, 0f, 1f);

        return 0f;
    }

    /// <summary>Altitude in z-stack coordinates, including fractional height.</summary>
    [PublicAPI]
    public float GetAbsoluteAltitude(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } mapUid)
            return 0f;

        // Open air between two levels: the ride starts at the lower anchor's depth.
        if (TryComp<CEZTransitMapComponent>(mapUid, out var transit))
        {
            var lowerDepth = transit.LowerMap is { } lower && _zMapQuery.TryComp(lower, out var lowerMap)
                ? lowerMap.Depth
                : 0;
            return lowerDepth + GetTransitProgress(transit) * GetTransitSpan(transit);
        }

        var depth = _zMapQuery.TryComp(mapUid, out var map) ? map.Depth : 0;
        return depth + GetLocalAltitude(uid);
    }

    [PublicAPI]
    public int GetTransitSpan(CEZTransitMapComponent transit)
    {
        if (_zMapQuery.TryComp(transit.LowerMap, out var lower) &&
            _zMapQuery.TryComp(transit.UpperMap, out var upper))
            return Math.Max(1, upper.Depth - lower.Depth);

        return 1;
    }

    /// <summary>
    /// How far a transit gap's ride has climbed from its lower anchor toward its
    /// upper one, 0..1.
    /// </summary>
    [PublicAPI]
    public float GetTransitProgress(CEZTransitMapComponent transit)
    {
        if (transit.PrimaryGrid is { } grid && ZPhysicsQuery.TryComp(grid, out var zPhys))
            return Math.Clamp(zPhys.LocalPosition, 0f, 1f);

        return 0f;
    }

    private void OnTileChanged(Entity<CEZMapComponent> ent, ref TileChangedEvent args)
    {
        if (!TryComp<MapGridComponent>(args.Entity, out var grid))
            return;

        foreach (var change in args.Changes)
        {
            var mapCoords = _map.GridTileToWorld(args.Entity, grid, change.GridIndices);
            var half = grid.TileSizeHalfVector;
            var min = mapCoords.Position - half;
            var max = mapCoords.Position + half;
            var aabb = new Box2(min, max);

            var entities = _lookup.GetEntitiesIntersecting(mapCoords.MapId, aabb, LookupFlags.Uncontained);
            foreach (var uid in entities)
            {
                if (!ZPhysicsQuery.TryComp(uid, out var zComp))
                    continue;

                RequestCacheMovement((uid, zComp));
                RefreshBody((uid, zComp));
            }
        }
    }

    private void RequestCacheMovement(Entity<CEZPhysicsComponent> entity, bool force = true)
    {
        var tile = _transform.GetGridOrMapTilePosition(entity);

        if (tile == entity.Comp.CachedTile && !force)
            return;

        entity.Comp.CachedTile = tile;
        entity.Comp.CachedGroundHeight = ComputeGroundHeightInternal((entity, entity), out var sticky);
        entity.Comp.CachedStickyGround = sticky;
    }

    private void OnMoveEvent(Entity<CEZPhysicsComponent> entity, ref MoveEvent args) => QueueGroundRefresh(entity);

    protected void QueueGroundRefresh(EntityUid entity)
    {
        if (_dirtyMovementBodies.Contains(entity))
            return;

        _dirtyMovementBodies.Add(entity);
    }

    private void OnZLevelMapMove(Entity<CEZPhysicsComponent> ent, ref CEZLevelMapMoveEvent args)
    {
        SyncCurrentZLevel(ent, args.CurrentZLevel);
        RequestCacheMovement(ent);
    }

    /// <summary>
    /// Computes the "ground height" relative to the entity's current Z-level.
    /// Returns values where 0 means ground on the same level, -1 means ground one level below,
    /// and intermediate values are possible for high ground entities (stairs).
    /// </summary>
    private float ComputeGroundHeightInternal(Entity<CEZPhysicsComponent?> target, out bool stickyGround, int maxFloors = 1)
    {
        stickyGround = false;

        if (!Resolve(target, ref target.Comp, false))
            return 0;

        var xform = Transform(target);
        if (!_zMapQuery.TryComp(xform.MapUid, out var zMapComp))
            return 0;

        var worldPos = _transform.GetWorldPosition(target);

        Entity<CEZMapComponent> checkingMap = (xform.MapUid.Value, zMapComp);

        for (var floor = 0; floor <= maxFloors; floor++)
        {
            if (floor != 0) //Select map below
            {
                if (!TryMapOffset((checkingMap.Owner, checkingMap.Comp), -floor, out var tempCheckingMap))
                    continue;

                checkingMap = tempCheckingMap;
            }

            if (!_map.TryFindGridAt(checkingMap, worldPos, out var gridUid, out var grid))
                continue;

            var gridTile = _map.WorldToTile(gridUid, grid, worldPos);

            var query = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, gridTile);
            while (query.MoveNext(out var uid))
            {
                if (!_zHighGroundQuery.TryComp(uid, out var heightComp))
                    continue;

                var dir = Transform(uid.Value).LocalRotation.GetCardinalDir();

                var gridLocal = _map.WorldToLocal(gridUid, grid, worldPos);
                var local = new Vector2((gridLocal.X % 1 + 1) % 1, (gridLocal.Y % 1 + 1) % 1);

                var t = dir switch
                {
                    Direction.East => heightComp.Corner ? (local.X + 1f - local.Y) / 2f : local.X,
                    Direction.West => heightComp.Corner ? (1f - local.X + local.Y) / 2f : 1f - local.X,
                    Direction.North => heightComp.Corner ? (local.X + local.Y) / 2f : local.Y,
                    Direction.South => heightComp.Corner ? (1f - local.X + 1f - local.Y) / 2f : 1f - local.Y,
                    _ => 0.5f,
                };

                t = float.Clamp(t, 0f, 1f);

                var curve = heightComp.HeightCurve;
                if (curve.Count == 0)
                    continue;

                if (curve.Count == 1)
                {
                    stickyGround = heightComp.Stick;
                    var groundY = curve[0];
                    // groundHeight is negative downwards: -floor + groundY
                    return -floor + groundY;
                }

                var step = 1f / (curve.Count - 1);
                var index = (int)(t / step);
                var frac = (t - index * step) / step;

                var y0 = curve[Math.Clamp(index, 0, curve.Count - 1)];
                var y1 = curve[Math.Clamp(index + 1, 0, curve.Count - 1)];

                var groundYInterp = MathHelper.Lerp(y0, y1, frac);

                stickyGround = heightComp.Stick;
                return -floor + groundYInterp;
            }

            if (_map.TryGetTileRef(gridUid, grid, gridTile, out var tileRef) &&
                !tileRef.Tile.IsEmpty)
                return -floor; // tile ground has groundY == 0 -> -floor
        }

        return -maxFloors;
    }

    [PublicAPI]
    public bool HasTileAbove(EntityUid ent, Entity<CEZMapComponent?>? currentMapUid = null)
    {
        currentMapUid ??= Transform(ent).MapUid;

        if (currentMapUid is null)
            return false;

        if (!TryMapUp(currentMapUid.Value, out var mapAboveUid))
            return false;

        var worldPos = _transform.GetWorldPosition(ent);
        if (!_map.TryFindGridAt(mapAboveUid, worldPos, out var gridUid, out var grid))
            return false;

        if (_map.TryGetTileRef(gridUid, grid, worldPos, out var tileRef) &&
            !tileRef.Tile.IsEmpty)
            return true;

        return false;
    }

    [PublicAPI]
    public bool HasTileAbove(Vector2i indices, Entity<CEZMapComponent?> map)
    {
        if (!Resolve(map, ref map.Comp, false))
            return false;

        if (!TryMapUp(map, out var mapAboveUid))
            return false;

        if (!_gridQuery.TryComp(mapAboveUid, out var mapAboveGrid))
            return false;

        if (_map.TryGetTileRef(mapAboveUid, mapAboveGrid, indices, out var tileRef) &&
            !tileRef.Tile.IsEmpty)
            return true;

        return false;
    }

    /// <summary>
    /// Checks whether any grid on the map above has a non-empty tile at the given world position.
    /// World-position overload; see also <see cref="HasTileAbove(EntityUid, Entity{CEZMapComponent?}?)"/>
    /// and <see cref="HasTileAbove(Vector2i, Entity{CEZMapComponent?})"/>.
    /// </summary>
    [PublicAPI]
    public bool HasTileAbove(Vector2 worldPos, Entity<CEZMapComponent?> currentMap)
    {
        if (!TryMapUp(currentMap, out var mapAboveUid))
            return false;

        if (!_map.TryFindGridAt(mapAboveUid, worldPos, out var gridUid, out var grid))
            return false;

        return _map.TryGetTileRef(gridUid, grid, worldPos, out var tileRef) && !tileRef.Tile.IsEmpty;
    }

    [PublicAPI]
    public void SetZPosition(Entity<CEZPhysicsComponent?> ent, float newPosition)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.LocalPosition = newPosition;
        DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.LocalPosition));
        WakeBody(ent);
    }

    [PublicAPI]
    public void SetLaunchCountdown(Entity<CEZPhysicsComponent?> ent, float seconds)
    {
        if (!Resolve(ent.Owner, ref ent.Comp) || ent.Comp.LaunchCountdown.Equals(seconds))
            return;

        ent.Comp.LaunchCountdown = seconds;
        DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.LaunchCountdown));
    }

    [PublicAPI]
    public void SetGroundContact(Entity<CEZPhysicsComponent?> ent, bool contact)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false) || ent.Comp.GroundContact == contact)
            return;

        ent.Comp.GroundContact = contact;
        DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.GroundContact));
    }

    [PublicAPI]
    public void UpdateGravityState(Entity<CEZPhysicsComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        var ev = new CECheckGravityEvent();
        RaiseLocalEvent(ent.Owner, ref ev);

        SetZGravity(ent, ev.Gravity);
    }

    private void SetZGravity(Entity<CEZPhysicsComponent?> ent, float newGravityMultiplier)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.GravityMultiplier = newGravityMultiplier;
        DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.GravityMultiplier));
        WakeBody(ent);
    }

    [PublicAPI]
    public void SetZVelocity(Entity<CEZPhysicsComponent?> ent, float newVelocity)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.Velocity = newVelocity;
        DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.Velocity));
        WakeBody(ent);
    }

    [PublicAPI]
    public void AddZVelocity(Entity<CEZPhysicsComponent?> ent, float newVelocity)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.Velocity += newVelocity;
        DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.Velocity));
        WakeBody(ent);
    }

    [PublicAPI]
    public bool TryMove(EntityUid ent, int offset, Entity<CEZMapComponent?>? map = null)
    {
        return TryMove(ent, offset, map, null);
    }

    public bool TryMoveToZDestination(EntityUid ent, int offset, MapId expectedDestination)
    {
        // A follower can cross from the ship's edge without first becoming map-parented.
        var transit = CompOrNull<CEZTransitMapComponent>(Transform(ent).MapUid);
        if (!TryMove(ent, offset, null, expectedDestination))
            return false;

        if (offset < 0 && transit != null &&
            ZPhysicsQuery.TryComp(ent, out var physics) && !physics.ProjectileFlight &&
            ZPhysicsQuery.TryComp(transit.PrimaryGrid, out var primaryPhysics))
            SetZPosition((ent, physics), primaryPhysics.LocalPosition);

        return true;
    }

    private bool TryMove(EntityUid ent, int offset, Entity<CEZMapComponent?>? map, MapId? expectedDestination)
    {
        var xform = Transform(ent);
        if (!IsDirectlyOnGridOrMap(xform))
            return false;

        map ??= xform.MapUid;

        if (map is null)
            return false;

        if (!TryMapOffset(map.Value, offset, out var targetMap))
            return false;

        if (!_mapQuery.TryComp(targetMap, out var targetMapComp))
            return false;

        // pzn: Transit maps have their own path.
        if (_gridQuery.TryComp(ent, out var gridComp) && !_mapQuery.HasComp(ent))
            return expectedDestination == null &&
                   TryMoveGrid((ent, gridComp), (targetMap.Owner, targetMap.Comp, targetMapComp), offset);

        var worldRot = _transform.GetWorldRotation(ent);
        var destMapId = targetMapComp.MapId;

        // Check for a transiting grid before falling to the next fixed level.
        if (offset < 0 && TryFindTransitLanding(ent, map.Value.Owner, targetMap.Owner, out var transitMapId))
            destMapId = transitMapId;

        if (expectedDestination is { } destination && destination != destMapId)
            return false;

        var beforeEv = new CEZLevelBeforeMapMoveEvent(offset, targetMap.Comp.Depth, destMapId);
        RaiseLocalEvent(ent, ref beforeEv);

        _transform.SetMapCoordinates(ent, new MapCoordinates(_transform.GetWorldPosition(ent), destMapId));
        _transform.SetWorldRotation(ent, worldRot);

        var ev = new CEZLevelMapMoveEvent(offset, targetMap.Comp.Depth);
        RaiseLocalEvent(ent, ref ev);

        return true;
    }

    private bool TryFindTransitLanding(EntityUid ent, EntityUid upperMap, EntityUid lowerMap, out MapId transitMapId)
    {
        transitMapId = MapId.Nullspace;
        var worldPos = _transform.GetWorldPosition(ent);

        var query = EntityQueryEnumerator<CEZTransitMapComponent, MapComponent>();
        while (query.MoveNext(out _, out var transit, out var mapComp))
        {
            if (transit.UpperMap != upperMap || transit.LowerMap != lowerMap)
                continue;

            if (!_map.TryFindGridAt(mapComp.MapId, worldPos, out _, out _))
                continue;

            transitMapId = mapComp.MapId;
            return true;
        }

        return false;
    }

    protected virtual bool TryMoveGrid(Entity<MapGridComponent> grid,
        Entity<CEZMapComponent, MapComponent> targetMap,
        int offset)
    {
        return false;
    }

    [PublicAPI]
    public bool TryMoveUp(EntityUid ent) => TryMove(ent, 1);

    [PublicAPI]
    public bool TryMoveDown(EntityUid ent)
    {
        return TryMove(ent, -1);
    }

    private void UpdateDirtyMovement()
    {
        for (var i = _dirtyMovementBodies.Count - 1; i >= 0; i--)
        {
            var uid = _dirtyMovementBodies[i];

            if (!ZPhysicsQuery.TryComp(uid, out var component))
                continue;

            var entity = (uid, component);
            RequestCacheMovement(entity);
            RefreshBody(entity);
        }

        _dirtyMovementBodies.Clear();
    }
}

[ByRefEvent]
public struct CEZLevelBeforeMapMoveEvent(int offset, int level, MapId? destination = null)
{
    /// <summary>
    /// How many levels were crossed. If negative, it means there was a downward movement. If positive, it means an upward movement.
    /// </summary>
    public int Offset = offset;

    public int CurrentZLevel = level;

    public MapId? Destination = destination;
}

[ByRefEvent]
public struct CEZLevelMapMoveEvent(int offset, int level)
{
    /// <summary>
    /// How many levels were crossed. If negative, it means there was a downward movement. If positive, it means an upward movement.
    /// </summary>
    public int Offset = offset;

    public int CurrentZLevel = level;
}

/// <summary>
/// Is triggered when an entity falls to the lower z-levels under the force of gravity
/// </summary>
[ByRefEvent]
public struct CEZLevelFallMapEvent;

/// <summary>
/// It is called on an entity when it hits the floor or ceiling with force.
/// </summary>
/// <param name="impactPower">The speed at the moment of impact. Always positive</param>
[ByRefEvent]
public struct CEZLevelHitEvent(float impactPower)
{
    public float ImpactPower = impactPower;
}

[ByRefEvent]
public struct CEGetZVelocityEvent(Entity<CEZPhysicsComponent> target, float frameTime)
{
    public Entity<CEZPhysicsComponent> Target = target;
    public float VelocityDelta = 0;

    /// <summary>
    /// The substep length the returned <see cref="VelocityDelta"/> is integrated over, so
    /// handlers can predict where this step lands and clamp against overshooting a plane.
    /// </summary>
    public float FrameTime = frameTime;
}

/// <summary>
/// Called when UpdateGravityState is used to update the current strength of the active z-level gravity. Various systems can subscribe to this to disable gravity.
/// </summary>
[ByRefEvent]
public struct CECheckGravityEvent()
{
    public float Gravity = 1f;
}

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
    private TimeSpan _accumulatedTime = TimeSpan.Zero;
    private readonly List<EntityUid> _dirtyMovementBodies = new();

    private void InitializeMovement()
    {
        SubscribeLocalEvent<CEZPhysicsComponent, CEZLevelMapMoveEvent>(OnZLevelMapMove);
        SubscribeLocalEvent<CEZPhysicsComponent, MoveEvent>(OnMoveEvent);
        SubscribeLocalEvent<CEZMapComponent, TileChangedEvent>(OnTileChanged);
    }

    /// <summary>
    /// Returns the last cached distance to the floor.
    /// </summary>
    /// <param name="target">The entity, the distance to the floor which we calculate</param>
    /// <returns></returns>
    [PublicAPI]
    public float DistanceToGround(Entity<CEZPhysicsComponent?> target)
    {
        if (!Resolve(target, ref target.Comp, false))
            return 0;

        return target.Comp.LocalPosition - target.Comp.CachedGroundHeight;
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

            var entities = _lookup.GetEntitiesIntersecting(mapCoords.MapId, aabb);
            foreach (var uid in entities)
            {
                if (!ZPhysicsQuery.TryComp(uid, out var zComp))
                    continue;

                RequestCacheMovement((uid, zComp));
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

    private void OnMoveEvent(Entity<CEZPhysicsComponent> entity, ref MoveEvent args)
    {
        if (_dirtyMovementBodies.Contains(entity))
            return;

        _dirtyMovementBodies.Add(entity);
    }

    private void OnZLevelMapMove(Entity<CEZPhysicsComponent> ent, ref CEZLevelMapMoveEvent args)
    {
        ent.Comp.CurrentZLevel = args.CurrentZLevel;
        DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.CurrentZLevel));
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

        //Select current map by default
        Entity<CEZMapComponent> checkingMap = (xform.MapUid.Value, zMapComp);

        for (var floor = 0; floor <= maxFloors; floor++)
        {
            if (floor != 0) //Select map below
            {
                if (!TryMapOffset((checkingMap.Owner, checkingMap.Comp), -floor, out var tempCheckingMap))
                    continue;

                checkingMap = tempCheckingMap;
            }

            //Find whichever grid (structure or planet) provides the floor here.
            if (!_mapManager.TryFindGridAt(checkingMap, worldPos, out var gridUid, out var grid))
                continue;

            var gridTile = _map.WorldToTile(gridUid, grid, worldPos);

            //Check all types of ZHeight entities
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

                if (target.Comp.Velocity < 0 && target.Comp.Velocity > -2f && heightComp.Stick)
                    stickyGround = true;

                return -floor + groundYInterp;
            }

            //No ZEntities found, check floor tiles
            if (_map.TryGetTileRef(gridUid, grid, gridTile, out var tileRef) &&
                !tileRef.Tile.IsEmpty)
                return -floor; // tile ground has groundY == 0 -> -floor
        }

        return -maxFloors;
    }

    /// <summary>
    /// Checks whether there is a ceiling above the specified entity (tiles on the layer above).
    /// If there are no Z-levels above, false will be returned.
    /// </summary>
    [PublicAPI]
    public bool HasTileAbove(EntityUid ent, Entity<CEZMapComponent?>? currentMapUid = null)
    {
        currentMapUid ??= Transform(ent).MapUid;

        if (currentMapUid is null)
            return false;

        if (!TryMapUp(currentMapUid.Value, out var mapAboveUid))
            return false;

        var worldPos = _transform.GetWorldPosition(ent);
        if (!_mapManager.TryFindGridAt(mapAboveUid, worldPos, out var gridUid, out var grid))
            return false;

        if (_map.TryGetTileRef(gridUid, grid, worldPos, out var tileRef) &&
            !tileRef.Tile.IsEmpty)
            return true;

        return false;
    }

    /// <summary>
    /// Checks whether there is a ceiling above the specified entity (tiles on the layer above).
    /// If there are no Z-levels above, false will be returned.
    /// </summary>
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

        if (!_mapManager.TryFindGridAt(mapAboveUid, worldPos, out var gridUid, out var grid))
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

    /// <summary>
    /// Sets the vertical velocity for the entity. Positive values make the entity fly upward. Negative values make it fly downward.
    /// </summary>
    [PublicAPI]
    public void SetZVelocity(Entity<CEZPhysicsComponent?> ent, float newVelocity)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.Velocity = newVelocity;
        DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.Velocity));
        WakeBody(ent);
    }

    /// <summary>
    /// Add the vertical velocity for the entity. Positive values make the entity fly upward. Negative values make it fly downward.
    /// </summary>
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
        map ??= Transform(ent).MapUid;

        if (map is null)
            return false;

        if (!TryMapOffset(map.Value, offset, out var targetMap))
            return false;

        if (!_mapQuery.TryComp(targetMap, out var targetMapComp))
            return false;

        var worldRot = _transform.GetWorldRotation(ent);

        _transform.SetMapCoordinates(ent, new MapCoordinates(_transform.GetWorldPosition(ent), targetMapComp.MapId));
        _transform.SetWorldRotation(ent, worldRot);

        var ev = new CEZLevelMapMoveEvent(offset, targetMap.Comp.Depth);
        RaiseLocalEvent(ent, ref ev);

        return true;
    }

    [PublicAPI]
    public bool TryMoveUp(EntityUid ent) => TryMove(ent, 1);

    [PublicAPI]
    public bool TryMoveDown(EntityUid ent)
    {
        return TryMove(ent, -1);
    }

    [PublicAPI]
    public bool TryMoveDownOrChasm(EntityUid ent)
    {
        if (TryMoveDown(ent))
            return true;

        //welp, that default Chasm behavior. Not really good, but ok for now.
        if (HasComp<ChasmFallingComponent>(ent))
            return false; //Already falling

        var audio = new SoundPathSpecifier("/Audio/Effects/falling.ogg");
        _audio.PlayPredicted(audio, Transform(ent).Coordinates, ent);
        var falling = AddComp<ChasmFallingComponent>(ent);
        falling.NextDeletionTime = _timing.CurTime + falling.DeletionTime;
        _blocker.UpdateCanMove(ent);

        return false;
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

/// <summary>
/// Is called on an entity when it moves between z-levels.
/// </summary>
/// <param name="offset">How many levels were crossed. If negative, it means there was a downward movement. If positive, it means an upward movement.</param>
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
    /// <summary>
    /// The speed at the moment of impact. Always positive
    /// </summary>
    public float ImpactPower = impactPower;
}

/// <summary>
/// Is called every frame to calculate the current vertical velocity of the active zphysics entities.
/// </summary>
[ByRefEvent]
public struct CEGetZVelocityEvent(Entity<CEZPhysicsComponent> target)
{
    public Entity<CEZPhysicsComponent> Target = target;
    public float VelocityDelta = 0;
}

/// <summary>
/// Called when UpdateGravityState is used to update the current strength of the active z-level gravity. Various systems can subscribe to this to disable gravity.
/// </summary>
[ByRefEvent]
public struct CECheckGravityEvent()
{
    public float Gravity = 1f;
}

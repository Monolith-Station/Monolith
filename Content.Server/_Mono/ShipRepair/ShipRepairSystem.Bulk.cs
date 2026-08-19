// Forge-Change-full
using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Server._Mono.ShipRepair;

public readonly record struct ShipRepairWork(
    Vector2 LocalPosition,
    Vector2i Tile,
    EntityUid? Entity,
    bool IsTile);

public sealed partial class ShipRepairSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    /// <summary>
    /// Counts snapshot entities that are missing or damaged, plus tiles that no longer match the snapshot.
    /// </summary>
    public int CountRepairTargets(EntityUid gridUid, ShipRepairDataComponent? data = null, MapGridComponent? grid = null)
    {
        if (!Resolve(gridUid, ref data, ref grid, false))
            return 0;

        var count = 0;
        foreach (var (chunkPos, chunk) in data.Chunks)
        {
            for (var x = 0; x < data.ChunkSize; x++)
            {
                for (var y = 0; y < data.ChunkSize; y++)
                {
                    var idx = x + y * data.ChunkSize;
                    var stored = chunk.Tiles[idx];
                    if (stored == Tile.Empty.TypeId)
                        continue;

                    var indices = chunkPos * data.ChunkSize + new Vector2i(x, y);
                    var current = _map.GetTileRef(gridUid, grid, indices).Tile.TypeId;
                    if (current != stored)
                        count++;
                }
            }

            foreach (var (_, spec) in chunk.Entities)
            {
                if (NeedsEntityRepair(gridUid, data, spec))
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Collects per-tile / per-entity repair work for the drydock map.
    /// </summary>
    public void CollectRepairWork(EntityUid gridUid, List<ShipRepairWork> dest, ShipRepairDataComponent? data = null, MapGridComponent? grid = null)
    {
        if (!Resolve(gridUid, ref data, ref grid, false))
            return;

        var tileSize = grid.TileSize;
        foreach (var (chunkPos, chunk) in data.Chunks)
        {
            for (var x = 0; x < data.ChunkSize; x++)
            {
                for (var y = 0; y < data.ChunkSize; y++)
                {
                    var idx = x + y * data.ChunkSize;
                    var stored = chunk.Tiles[idx];
                    if (stored == Tile.Empty.TypeId)
                        continue;

                    var indices = chunkPos * data.ChunkSize + new Vector2i(x, y);
                    var current = _map.GetTileRef(gridUid, grid, indices).Tile.TypeId;
                    if (current == stored)
                        continue;

                    dest.Add(new ShipRepairWork(
                        new Vector2((indices.X + 0.5f) * tileSize, (indices.Y + 0.5f) * tileSize),
                        indices,
                        null,
                        true));
                }
            }

            foreach (var (_, spec) in chunk.Entities)
            {
                if (!NeedsEntityRepair(gridUid, data, spec, out var origUid, out _))
                    continue;

                var tile = _map.LocalToTile(gridUid, grid, new EntityCoordinates(gridUid, spec.LocalPosition));
                dest.Add(new ShipRepairWork(
                    spec.LocalPosition,
                    tile,
                    origUid is { } uid && !TerminatingOrDeleted(uid) ? uid : null,
                    false));
            }
        }
    }

    /// <summary>
    /// Applies a single map-marked repair: restore one tile, respawn a missing snapshot entity, or heal damage.
    /// </summary>
    public int TryApplyRepairWork(EntityUid gridUid, Vector2i tile, Vector2 localPos, EntityUid? entity, bool isTile)
    {
        if (isTile)
        {
            if (!TryComp<ShipRepairDataComponent>(gridUid, out var tileData) ||
                !TryComp<MapGridComponent>(gridUid, out var tileGrid) ||
                !TryGetChunk(tileData, tile, out var chunk))
                return 0;

            var rel = GetRelativeIndices(tile, tileData.ChunkSize);
            var stored = chunk.Tiles[rel.X + rel.Y * tileData.ChunkSize];
            if (stored == Tile.Empty.TypeId)
                return 0;

            var current = _map.GetTileRef(gridUid, tileGrid, tile).Tile.TypeId;
            if (current == stored)
                return 0;

            return TryRepairTileTile((gridUid, tileData), tile) ? 1 : 0;
        }

        if (TryComp<ShipRepairDataComponent>(gridUid, out var data) &&
            TryRepairSnapshotTarget(gridUid, data, entity, localPos))
            return 1;

        if (entity is { } uid &&
            !TerminatingOrDeleted(uid) &&
            !HasComp<MobStateComponent>(uid) &&
            TryComp<DamageableComponent>(uid, out var damageable) &&
            damageable.TotalDamage > 0)
        {
            _damageable.SetAllDamage(uid, damageable, 0);
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Restores missing snapshot entities and tiles, and heals damaged originals that are still in place.
    /// Destroyed upgraded walls come back as their original (base) prototypes from the snapshot.
    /// </summary>
    public int RepairFromSnapshot(EntityUid gridUid, ShipRepairDataComponent? data = null, MapGridComponent? grid = null)
    {
        if (!Resolve(gridUid, ref data, ref grid, false))
            return 0;

        var repaired = 0;
        var tileSet = new List<(Vector2i, Tile)>();
        foreach (var (chunkPos, chunk) in data.Chunks)
        {
            for (var x = 0; x < data.ChunkSize; x++)
            {
                for (var y = 0; y < data.ChunkSize; y++)
                {
                    var idx = x + y * data.ChunkSize;
                    var stored = chunk.Tiles[idx];
                    if (stored == Tile.Empty.TypeId)
                        continue;

                    var indices = chunkPos * data.ChunkSize + new Vector2i(x, y);
                    var current = _map.GetTileRef(gridUid, grid, indices).Tile.TypeId;
                    if (current == stored)
                        continue;

                    tileSet.Add((indices, new Tile(stored)));
                    repaired++;
                }
            }
        }

        if (tileSet.Count > 0)
            _map.SetTiles(gridUid, grid, tileSet);

        foreach (var (_, chunk) in data.Chunks)
        {
            foreach (var (_, spec) in chunk.Entities)
            {
                if (ApplyEntitySpec(gridUid, data, spec))
                    repaired++;
            }
        }

        Dirty(gridUid, data);
        return repaired;
    }

    /// <summary>
    /// Points a snapshot slot at a replacement entity (e.g. an upgraded wall) so repair heals it
    /// instead of respawning the original prototype on top of it.
    /// </summary>
    public void RetargetSnapshotEntity(EntityUid gridUid, EntityUid oldUid, EntityUid newUid, ShipRepairDataComponent? data = null)
    {
        if (!Resolve(gridUid, ref data, false))
            return;

        var oldNet = GetNetEntity(oldUid);
        var newNet = GetNetEntity(newUid);
        var changed = false;
        foreach (var chunk in data.Chunks.Values)
        {
            foreach (var spec in chunk.Entities.Values)
            {
                if (spec.OriginalEntity != oldNet)
                    continue;

                spec.OriginalEntity = newNet;
                changed = true;
            }
        }

        if (changed)
            Dirty(gridUid, data);
    }

    private bool TryRepairSnapshotTarget(
        EntityUid gridUid,
        ShipRepairDataComponent data,
        EntityUid? entity,
        Vector2 localPos)
    {
        foreach (var chunk in data.Chunks.Values)
        {
            foreach (var spec in chunk.Entities.Values)
            {
                var matchEntity = entity != null && spec.OriginalEntity == GetNetEntity(entity.Value);
                var matchPos = (spec.LocalPosition - localPos).LengthSquared() <= 0.26f;
                if (!matchEntity && !matchPos)
                    continue;

                if (!NeedsEntityRepair(gridUid, data, spec))
                    continue;

                return ApplyEntitySpec(gridUid, data, spec);
            }
        }

        return false;
    }

    private bool ApplyEntitySpec(EntityUid gridUid, ShipRepairDataComponent data, ShipRepairEntitySpecifier spec)
    {
        if (!NeedsEntityRepair(gridUid, data, spec, out var origUid, out var healOnly))
            return false;

        if (healOnly && origUid != null)
        {
            if (TryComp<DamageableComponent>(origUid.Value, out var damageable) && damageable.TotalDamage > 0)
            {
                _damageable.SetAllDamage(origUid.Value, damageable, 0);
                return true;
            }

            return false;
        }

        if (origUid != null && !TerminatingOrDeleted(origUid.Value))
            QueueDel(origUid.Value);

        var protoId = data.EntityPalette[spec.ProtoIndex];
        var coords = new EntityCoordinates(gridUid, spec.LocalPosition);
        var spawned = Spawn(protoId, coords);
        _transform.SetLocalRotation(spawned, spec.Rotation);
        spec.OriginalEntity = GetNetEntity(spawned);
        Dirty(gridUid, data);
        return true;
    }

    private bool NeedsEntityRepair(EntityUid gridUid, ShipRepairDataComponent data, ShipRepairEntitySpecifier spec)
    {
        return NeedsEntityRepair(gridUid, data, spec, out _, out _);
    }

    private bool NeedsEntityRepair(
        EntityUid gridUid,
        ShipRepairDataComponent data,
        ShipRepairEntitySpecifier spec,
        out EntityUid? origUid,
        out bool healOnly)
    {
        origUid = spec.OriginalEntity == null ? null : GetEntity(spec.OriginalEntity.Value);
        healOnly = false;

        if (origUid == null || TerminatingOrDeleted(origUid.Value) || Transform(origUid.Value).GridUid != gridUid)
        {
            if (TryFindOccupant(gridUid, spec.LocalPosition, out var occupant))
            {
                spec.OriginalEntity = GetNetEntity(occupant);
                origUid = occupant;
                Dirty(gridUid, data);
            }
            else
            {
                return true;
            }
        }

        var origXform = Transform(origUid.Value);
        var coords = new EntityCoordinates(gridUid, spec.LocalPosition);
        if (origXform.Coordinates.TryDistance(EntityManager, coords, out var distance) && distance > 0.5f)
        {
            if (TryFindOccupant(gridUid, spec.LocalPosition, out var occupant))
            {
                spec.OriginalEntity = GetNetEntity(occupant);
                origUid = occupant;
                origXform = Transform(occupant);
                Dirty(gridUid, data);
            }
            else
            {
                return true;
            }
        }

        if (TryComp<DamageableComponent>(origUid.Value, out var damageable) && damageable.TotalDamage > 0)
        {
            healOnly = true;
            return true;
        }

        return false;
    }

    private bool TryFindOccupant(EntityUid gridUid, System.Numerics.Vector2 localPos, out EntityUid occupant)
    {
        occupant = default;
        var coords = new EntityCoordinates(gridUid, localPos);
        var candidates = new HashSet<Entity<ShipRepairableComponent>>();
        _entityLookup.GetEntitiesInRange(coords, 0.51f, candidates);
        foreach (var ent in candidates)
        {
            if (TerminatingOrDeleted(ent) || ent.Owner == gridUid)
                continue;

            var xform = Transform(ent);
            if (xform.ParentUid != gridUid || !xform.Anchored)
                continue;

            if (!xform.Coordinates.TryDistance(EntityManager, coords, out var dist) || dist > 0.5f)
                continue;

            occupant = ent;
            return true;
        }

        return false;
    }
}

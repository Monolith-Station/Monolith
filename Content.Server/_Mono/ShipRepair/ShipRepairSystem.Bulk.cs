// Forge-Change-full
using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.ShipRepair;

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
                if (!NeedsEntityRepair(gridUid, data, spec, out var origUid, out var healOnly))
                    continue;

                if (healOnly && origUid != null)
                {
                    if (TryComp<DamageableComponent>(origUid.Value, out var damageable) && damageable.TotalDamage > 0)
                    {
                        _damageable.SetAllDamage(origUid.Value, damageable, 0);
                        repaired++;
                    }

                    continue;
                }

                if (origUid != null && !TerminatingOrDeleted(origUid.Value))
                    QueueDel(origUid.Value);

                var protoId = data.EntityPalette[spec.ProtoIndex];
                var coords = new EntityCoordinates(gridUid, spec.LocalPosition);
                var spawned = Spawn(protoId, coords);
                _transform.SetLocalRotation(spawned, spec.Rotation);
                spec.OriginalEntity = GetNetEntity(spawned);
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

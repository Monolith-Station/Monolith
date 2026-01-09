using Content.Shared._Mono.ForceParent;
using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared._Mono.ShipRepair;

public abstract partial class SharedShipRepairSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly ForceParentSystem _parent = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly INetManager _net = default!; // .IsServer is kind of a crime but needed to not dupe code
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitTool();
    }

    /// <summary>
    /// Generate snapshot of grid repair data and store on grid.
    /// </summary>
    public void GenerateRepairData(EntityUid gridUid)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var repairData = EnsureComp<ShipRepairDataComponent>(gridUid);
        repairData.Chunks.Clear();
        repairData.EntityPalette.Clear();

        var chunkSize = repairData.ChunkSize;

        // tile snapshot
        var tiles = _map.GetAllTilesEnumerator(gridUid, grid);
        while (tiles.MoveNext(out var mTileRef))
        {
            if (mTileRef == null)
                continue;
            var tileRef = mTileRef.Value;

            var gridIndices = tileRef.GridIndices;
            var chunk = GetCreateChunk(repairData, gridIndices);

            var rel = GetRelativeIndices(gridIndices, chunkSize);
            chunk.Tiles[rel.X + rel.Y * chunkSize] = tileRef.Tile.TypeId;
        }

        // entities snapshot
        var repairables = new HashSet<Entity<ShipRepairableComponent>>();
        _lookup.GetLocalEntitiesIntersecting(gridUid, grid.LocalAABB, repairables);
        foreach (var childEnt in repairables)
        {
            if (TerminatingOrDeleted(childEnt))
                continue;

            var childXform = Transform(childEnt);
            // only ents directly parented to grid and anchored
            if (childXform.ParentUid != gridUid || !childXform.Anchored)
                continue;

            var query = new ShipRepairStoreQueryEvent(true);
            RaiseLocalEvent(childEnt, ref query);
            if (!query.Repairable)
                continue;

            var maybeProtoId = childEnt.Comp.RepairTo;
            if (maybeProtoId == null)
            {
                var meta = MetaData(childEnt);
                if (meta.EntityPrototype == null)
                    continue;
                maybeProtoId = new EntProtoId(meta.EntityPrototype.ID);
            }
            var protoId = maybeProtoId.Value;

            var paletteIndex = repairData.EntityPalette.IndexOf(protoId);
            if (paletteIndex == -1)
            {
                repairData.EntityPalette.Add(protoId);
                paletteIndex = repairData.EntityPalette.Count - 1;
            }

            var localPos = childXform.LocalPosition;
            var gridIndices = _map.LocalToTile(gridUid, grid, childXform.Coordinates);
            var chunk = GetCreateChunk(repairData, gridIndices);

            chunk.Entities[chunk.NextUid++] = new ShipRepairEntitySpecifier
            {
                ProtoIndex = paletteIndex,
                OriginalEntity = GetNetEntity(childEnt),
                Rotation = childXform.LocalRotation,
                LocalPosition = localPos
            };
        }

        Dirty(gridUid, repairData);
    }

    public bool TryRepairTileTile(Entity<ShipRepairDataComponent> grid, Vector2i indices)
    {
        if (!TryGetChunk(grid.Comp, indices, out var chunk) || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var relative = GetRelativeIndices(indices, grid.Comp.ChunkSize);
        var idx = relative.X + relative.Y * grid.Comp.ChunkSize;

        var tileToPlace = chunk.Tiles[idx];
        if (tileToPlace != Tile.Empty.TypeId)
            _map.SetTile(grid, gridComp, indices, new Tile(tileToPlace));
        return true;
    }

    protected Vector2i GetRepairChunkIndices(Vector2i gridIndices, int chunkSize)
    {
        var xCoord = gridIndices.X < 0 ? 1 - chunkSize + gridIndices.X : gridIndices.X;
        var yCoord = gridIndices.Y < 0 ? 1 - chunkSize + gridIndices.Y : gridIndices.Y;
        var x = xCoord / chunkSize;
        var y = yCoord / chunkSize;
        return new Vector2i(x, y);
    }

    protected Vector2i GetRelativeIndices(Vector2i gridIndices, int chunkSize)
    {
        var x = MathHelper.Mod(gridIndices.X, chunkSize);
        var y = MathHelper.Mod(gridIndices.Y, chunkSize);
        return new Vector2i(x, y);
    }

    protected ShipRepairChunk GetCreateChunk(ShipRepairDataComponent data, Vector2i gridIndices)
    {
        var chunkSize = data.ChunkSize;
        var chunkIndices = GetRepairChunkIndices(gridIndices, chunkSize);

        if (!data.Chunks.TryGetValue(chunkIndices, out var chunk))
        {
            chunk = new ShipRepairChunk
            {
                Tiles = new int[chunkSize * chunkSize]
            };
            Array.Fill<int>(chunk.Tiles, Tile.Empty.TypeId);
            data.Chunks[chunkIndices] = chunk;
        }

        return chunk;
    }

    protected bool TryGetChunk(ShipRepairDataComponent data, Vector2i gridIndices, [NotNullWhen(true)] out ShipRepairChunk? chunk)
    {
        var chunkSize = data.ChunkSize;
        var chunkIndices = GetRepairChunkIndices(gridIndices, chunkSize);
        return data.Chunks.TryGetValue(chunkIndices, out chunk);
    }
}

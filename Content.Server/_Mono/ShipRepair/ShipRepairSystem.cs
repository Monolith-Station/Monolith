using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared._Mono.ShipRepair;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Server._Mono.ShipRepair;

public sealed partial class ShipRepairSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShipRepairToolComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ShipRepairToolComponent, ShipRepairDoAfterEvent>(OnRepairDoAfter);
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

        var xform = Transform(gridUid);
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
        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var childUid))
        {
            if (TerminatingOrDeleted(childUid))
                continue;

            var childXform = Transform(childUid);
            // only ents directly parented to grid and anchored
            if (childXform.ParentUid != gridUid || !childXform.Anchored)
                continue;

            var query = new ShipRepairStoreQueryEvent(true);
            RaiseLocalEvent(childUid, ref query);
            if (!query.Repairable)
                continue;

            var meta = MetaData(childUid);
            if (meta.EntityPrototype == null)
                continue;
            var protoId = new EntProtoId(meta.EntityPrototype.ID);

            var paletteIndex = repairData.EntityPalette.IndexOf(protoId);
            if (paletteIndex == -1)
            {
                repairData.EntityPalette.Add(protoId);
                paletteIndex = repairData.EntityPalette.Count - 1;
            }

            var localPos = childXform.LocalPosition;
            var gridIndices = _map.LocalToTile(gridUid, grid, childXform.Coordinates);
            var chunk = GetCreateChunk(repairData, gridIndices);

            chunk.Entities.Add(new ShipRepairEntitySpecifier
            {
                ProtoIndex = paletteIndex,
                OriginalEntity = childUid,
                Rotation = childXform.LocalRotation,
                LocalPosition = localPos
            });
        }
    }

    private void OnAfterInteract(Entity<ShipRepairToolComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        // TODO: find grids near click instead of using user grid
        var maybeTargetGrid = Transform(ent).GridUid;
        if (!TryComp<MapGridComponent>(maybeTargetGrid, out var gridComp))
            return;
        var targetGrid = maybeTargetGrid.Value;

        if (!TryComp<ShipRepairDataComponent>(targetGrid, out var repairData))
        {
            GenerateRepairData(targetGrid);
            return;
        }

        var clickPos = args.ClickLocation;
        var gridIndices = _map.CoordinatesToTile(targetGrid, gridComp, clickPos);

        if (!TryGetChunk(repairData, gridIndices, out var chunk))
            return;

        // first try repair tile if we can
        if (ent.Comp.EnableTileRepair)
        {
            var relativeIndices = GetRelativeIndices(gridIndices, repairData.ChunkSize);
            var index = relativeIndices.X + relativeIndices.Y * repairData.ChunkSize;

            var storedTile = chunk.Tiles[index];
            var currentTile = _map.GetTileRef(targetGrid, gridComp, gridIndices).Tile;

            // don't repair to space or a tile that existss
            if (storedTile != Tile.Empty.TypeId && currentTile.IsEmpty)
            {
                StartRepair(ent, args.User, targetGrid, gridIndices);
                return; // do not attempt anything else
            }
        }

        // try entity repair if we haven't done tile repair
        if (ent.Comp.EnableEntityRepair)
        {
            for (var i = 0; i < chunk.Entities.Count; i++)
            {
                var spec = chunk.Entities[i];

                // only consider it if it's close enough
                if ((spec.LocalPosition - clickPos.Position).Length() > ent.Comp.EntitySearchRadius)
                    continue;

                var needsRepair = true;
                if (spec.OriginalEntity != null && !TerminatingOrDeleted(spec.OriginalEntity))
                {
                    // if it's still on our grid, don't repair
                    // TODO: decide how to do this better to not have to use the DRM thing for everything
                    var origXform = Transform(spec.OriginalEntity.Value);
                    if (origXform.GridUid == targetGrid)
                        continue;

                    var ev = new ShipRepairReinstateQueryEvent(true);
                    RaiseLocalEvent(spec.OriginalEntity.Value, ref ev);
                    needsRepair = ev.Repairable;
                }

                if (needsRepair)
                {
                    StartRepair(ent, args.User, targetGrid, gridIndices, true, i);
                    return;
                }
            }
        }
    }

    private void StartRepair(Entity<ShipRepairToolComponent> tool, EntityUid user, EntityUid grid, Vector2i tileIndices, bool isEntity = false, int entityIndex = 0)
    {
        _audio.PlayPvs(tool.Comp.RepairSound, tool);

        var ev = new ShipRepairDoAfterEvent
        {
            TargetGridIndices = tileIndices,
            IsEntityRepair = isEntity,
            EntitySpecifierIndex = entityIndex
        };

        var delay = isEntity ? tool.Comp.EntityRepairTime : tool.Comp.TileRepairTime;
        var args = new DoAfterArgs(EntityManager, user, delay, ev, tool, grid)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnRepairDoAfter(Entity<ShipRepairToolComponent> ent, ref ShipRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Target is not { } targetGrid || !TryComp<ShipRepairDataComponent>(targetGrid, out var repairData))
            return;

        if (!TryComp<MapGridComponent>(targetGrid, out var gridComp))
            return;

        if (!TryGetChunk(repairData, args.TargetGridIndices, out var chunk))
            return;

        if (args.IsEntityRepair)
        {
            if (args.EntitySpecifierIndex < 0 || args.EntitySpecifierIndex >= chunk.Entities.Count)
                return;

            var spec = chunk.Entities[args.EntitySpecifierIndex];

            var protoId = repairData.EntityPalette[spec.ProtoIndex];
            var coords = new EntityCoordinates(targetGrid, spec.LocalPosition);

            var spawned = Spawn(protoId, coords);
            _transform.SetLocalRotation(spawned, spec.Rotation);

            spec.OriginalEntity = spawned;
        }
        else
        {
            var relative = GetRelativeIndices(args.TargetGridIndices, repairData.ChunkSize);
            var idx = relative.X + relative.Y * repairData.ChunkSize;

            if (idx >= 0 && idx < chunk.Tiles.Length)
            {
                var tileToPlace = chunk.Tiles[idx];
                if (tileToPlace != Tile.Empty.TypeId)
                {
                    _map.SetTile(targetGrid, gridComp, args.TargetGridIndices, new Tile(tileToPlace));
                }
            }
        }

        args.Handled = true;
    }

    private Vector2i GetRepairChunkIndices(Vector2i gridIndices, int chunkSize)
    {
        var xCoord = gridIndices.X < 0 ? -4 + gridIndices.X : gridIndices.X;
        var yCoord = gridIndices.Y < 0 ? -4 + gridIndices.Y : gridIndices.Y;
        var x = xCoord / chunkSize;
        var y = yCoord / chunkSize;
        return new Vector2i(x, y);
    }

    private Vector2i GetRelativeIndices(Vector2i gridIndices, int chunkSize)
    {
        var x = MathHelper.Mod(gridIndices.X, chunkSize);
        var y = MathHelper.Mod(gridIndices.Y, chunkSize);
        return new Vector2i(x, y);
    }

    private ShipRepairChunk GetCreateChunk(ShipRepairDataComponent data, Vector2i gridIndices)
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

    private bool TryGetChunk(ShipRepairDataComponent data, Vector2i gridIndices, [NotNullWhen(true)] out ShipRepairChunk? chunk)
    {
        var chunkSize = data.ChunkSize;
        var chunkIndices = GetRepairChunkIndices(gridIndices, chunkSize);
        return data.Chunks.TryGetValue(chunkIndices, out chunk);
    }
}

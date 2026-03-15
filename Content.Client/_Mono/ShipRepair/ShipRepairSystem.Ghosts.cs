using Content.Client.IconSmoothing;
using Content.Shared._Mono.ShipRepair;
using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.DrawDepth;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client._Mono.ShipRepair;

public sealed partial class ShipRepairSystem : SharedShipRepairSystem
{
    // active ghost entities so we can clean them up, this could be an EQE instead but performance
    private readonly Dictionary<GhostPosData, EntityUid> _activeGhosts = new();

    private readonly HashSet<GhostPosData> _visibleGhosts = new();

    private Color ghostColor = new Color(255, 128, 0, 128);

    private EntProtoId RepairGhostId = "RepairGhost";

    private ITileDefinition? PlatingDef = default!;

    private void InitGhosts()
    {
        PlatingDef = _tileDefs["Plating"];
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var player = _player.LocalEntity;
        if (player == null || !HasComp<RepairDataEyeComponent>(player))
        {
            ClearGhosts();
            return;
        }

        float maxRange = 0f;
        foreach (var hand in _hands.EnumerateHands(player.Value))
            if (_toolQuery.TryComp(hand.HeldEntity, out var tool) && tool.GhostRenderRadius > maxRange)
                maxRange = tool.GhostRenderRadius;

        if (maxRange <= 0f)
        {
            ClearGhosts();
            return;
        }

        _visibleGhosts.Clear();

        var playerXform = Transform(player.Value);
        var playerMapPos = _transform.GetMapCoordinates(player.Value, playerXform);

        // find all grids in range so it displays for nearby grids and not just our own
        var searchBox = Box2.CenteredAround(playerMapPos.Position, new Vector2(maxRange * 2, maxRange * 2));
        var grids = new List<Entity<MapGridComponent>>();
        _mapMan.FindGridsIntersecting(playerMapPos.MapId, searchBox, ref grids, true, false);

        var shiftVec = new Vector2(maxRange, maxRange);

        // might be a bit evil performance-wise but not sure how to do it otherwise
        foreach (var grid in grids)
        {
            if (!_dataQuery.TryComp(grid, out var data))
                continue;

            var localPos = _transform.WithEntityId(_transform.ToCoordinates(playerMapPos), grid);

            // search for all chunks we should look in in a square
            var minTile = _map.LocalToTile(grid, grid, localPos.Offset(-shiftVec));
            var maxTile = _map.LocalToTile(grid, grid, localPos.Offset(shiftVec));

            var minChunk = GetRepairChunkIndices(minTile, data.ChunkSize);
            var maxChunk = GetRepairChunkIndices(maxTile, data.ChunkSize);

            for (var x = minChunk.X; x <= maxChunk.X; x++)
            {
                for (var y = minChunk.Y; y <= maxChunk.Y; y++)
                {
                    var chunkIndices = new Vector2i(x, y);
                    if (!data.Chunks.TryGetValue(chunkIndices, out var chunk))
                        continue;

                    // process entity ghosts
                    foreach (var (specId, spec) in chunk.Entities)
                    {
                        var origUid = spec.OriginalEntity == null ? (EntityUid?)null : GetEntity(spec.OriginalEntity.Value);
                        // this will get trolled by PVS but hope repairable entities aren't too often on the same grid but at a far position
                        if (origUid != null && !TerminatingOrDeleted(origUid) && Transform(origUid.Value).GridUid == grid.Owner)
                            continue;

                        var specCoords = new EntityCoordinates(grid, spec.LocalPosition);
                        var specMapPos = _transform.ToMapCoordinates(specCoords);

                        // check if it's actually in range
                        if (specMapPos.MapId != playerMapPos.MapId
                            || (specMapPos.Position - playerMapPos.Position).LengthSquared() > maxRange * maxRange
                        )
                            continue;

                        _visibleGhosts.Add(new((grid, data, grid.Comp), chunkIndices, specId, false));
                    }

                    // process tile ghosts
                    for (int i = 0; i < chunk.Tiles.Length; i++)
                    {
                        var storedTileId = chunk.Tiles[i];
                        if (storedTileId == Tile.Empty.TypeId)
                            continue;

                        var rx = i % data.ChunkSize;
                        var ry = i / data.ChunkSize;
                        var tileIndices = chunkIndices * data.ChunkSize + new Vector2i(rx, ry);

                        // all good, no ghost
                        var currentTile = _map.GetTileRef(grid, grid, tileIndices).Tile;
                        if (currentTile.TypeId == storedTileId)
                            continue;

                        var tileLocal = _map.TileCenterToVector(grid, tileIndices);
                        var tileCoords = new EntityCoordinates(grid, tileLocal);
                        var tileMapPos = _transform.ToMapCoordinates(tileCoords);

                        // check out of range
                        if (tileMapPos.MapId != playerMapPos.MapId
                            || (tileMapPos.Position - playerMapPos.Position).LengthSquared() > maxRange * maxRange
                        )
                            continue;

                        _visibleGhosts.Add(new((grid, data, grid.Comp), chunkIndices, i, true));
                    }
                }
            }
        }

        // check which ghosts went out of range
        var toRemove = new List<GhostPosData>();
        foreach (var key in _activeGhosts.Keys)
            if (!_visibleGhosts.Contains(key))
                toRemove.Add(key);

        foreach (var key in toRemove)
        {
            QueueDel(_activeGhosts[key]);
            _activeGhosts.Remove(key);
        }

        foreach (var key in _visibleGhosts)
        {
            if (_activeGhosts.ContainsKey(key))
                continue;

            var (grid, chunkIdx, id, isTile) = key;

            if (grid.Comp1.Chunks.TryGetValue(chunkIdx, out var chunk))
            {
                if (isTile)
                {
                    var tileId = chunk.Tiles[id];
                    var rx = id % grid.Comp1.ChunkSize;
                    var ry = id / grid.Comp1.ChunkSize;
                    var tileIndices = chunkIdx * grid.Comp1.ChunkSize + new Vector2i(rx, ry);

                    SpawnTileGhost(key, tileIndices, (ushort)tileId);
                }
                else if (chunk.Entities.TryGetValue(id, out var spec))
                {
                    var protoId = grid.Comp1.EntityPalette[spec.ProtoIndex];
                    SpawnEntityGhost(key, spec, protoId);
                }
            }
        }
    }

    private void ClearGhosts()
    {
        foreach (var uid in _activeGhosts.Values)
            QueueDel(uid);

        _activeGhosts.Clear();
    }

    private void SpawnEntityGhost(GhostPosData key, ShipRepairEntitySpecifier spec, EntProtoId protoId)
    {
        if (_proto.TryIndex(protoId, out var proto)
            && proto.TryGetComponent<SpriteComponent>(out var specSprite, Factory))
        {
            // needed so it doesn't fall off if offgrid
            var ghost = Spawn(RepairGhostId, new EntityCoordinates(key.Grid, Vector2.Zero));
            _parent.SetForceParent(ghost, new EntityCoordinates(key.Grid, spec.LocalPosition));
            _transform.SetLocalRotationNoLerp(ghost, spec.Rotation);

            var sprite = _serialization.CreateCopy(specSprite, notNullableOverride: true);
            AddComp(ghost, sprite);
            var ent = (ghost, sprite);

            // evil hacks to not trip debug asserts
            var old = specSprite.Owner;
            specSprite.Owner = ghost;
            _sprite.CopySprite((ghost, specSprite), ent);
            specSprite.Owner = old;

            if (proto.TryGetComponent<IconSmoothComponent>(out var specSmooth, Factory))
            {
                var smooth = _serialization.CreateCopy(specSmooth, notNullableOverride: true);
                AddComp(ghost, smooth);
            }

            _sprite.SetColor(ent, ghostColor);
            _sprite.SetVisible(ent, true);
            _sprite.SetDrawDepth(ent, (int)Content.Shared.DrawDepth.DrawDepth.Objects);

            var i = 0;
            foreach (var layer in sprite.AllLayers)
            {
                sprite.LayerSetShader(i, "unshaded");
                i++;
            }

            _metaData.SetEntityName(ghost, Loc.GetString("repair-ghost-name", ("proto", proto.Name)));

            _activeGhosts[key] = ghost;
        }
    }

    private void SpawnTileGhost(GhostPosData key, Vector2i indices, ushort tileId)
    {
        if (_tileDefs.TryGetDefinition(tileId, out var def)
            && def is ContentTileDefinition tileDef)
        {
            var localPos = _map.TileCenterToVector((key.Grid, key.Grid.Comp2), indices);
            var ghost = Spawn(RepairGhostId, new EntityCoordinates(key.Grid, Vector2.Zero));
            _parent.SetForceParent(ghost, new EntityCoordinates(key.Grid, localPos));

            var sprite = EnsureComp<SpriteComponent>(ghost);
            var ent = (ghost, sprite);

            // render everything with plating sprite because tile sprites are evil and are a string of their variants
            if (PlatingDef?.Sprite != null)
            {
                var layer = _sprite.AddBlankLayer(ent, 0);
                _sprite.LayerSetTexture(ent, 0, PlatingDef.Sprite.Value);
                _sprite.LayerSetVisible(layer, true);
                sprite.LayerSetShader(0, "unshaded");
            }

            _sprite.SetColor(ghost, ghostColor);
            _sprite.SetDrawDepth(ghost, (int)Content.Shared.DrawDepth.DrawDepth.FloorObjects);

            _metaData.SetEntityName(ghost, Loc.GetString("repair-ghost-name", ("proto", Loc.GetString(tileDef.Name))));

            _activeGhosts[key] = ghost;
        }
    }

    private record struct GhostPosData(Entity<ShipRepairDataComponent, MapGridComponent> Grid, Vector2i ChunkIndices, int Id, bool IsTile);
}

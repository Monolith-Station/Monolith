/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CE.ZLevels.Roof;

/// <summary>
/// Systems that automatically covers tiles with roofs (or removes roofs)
/// if there is a tile on one of the levels above in the ZLevels Map or Grid network.
/// </summary>
public abstract partial class CESharedZLevelsRoofSystem : EntitySystem
{
    [Dependency] protected CESharedZLevelsSystem ZLevel = null!;
    [Dependency] protected SharedRoofSystem Roof = null!;
    [Dependency] protected SharedMapSystem Map = null!;
    [Dependency] protected ITileDefinitionManager TilDefMan = null!;

    [Dependency] protected EntityQuery<MapGridComponent> GridQuery = default!;
    [Dependency] protected EntityQuery<RoofComponent> RoofQuery = default!;
    [Dependency] protected EntityQuery<CEZMapComponent> ZMapQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelRoofComponent, TileChangedEvent>(OnTileChanged);
    }

    private void OnTileChanged(Entity<CEZLevelRoofComponent> ent, ref TileChangedEvent args)
    {
        if (!GridQuery.TryComp(ent, out var currentMapGrid))
            return;
        if (!RoofQuery.TryComp(ent, out var currentRoof))
            return;

        if (args.Changes.Length == 0)
            return;

        if (ZMapQuery.TryComp(ent, out var zLevelMapComp))
            OnMapTileChanged(ent, currentMapGrid, currentRoof, zLevelMapComp, args);
        else
            OnGridTileChanged(ent, currentMapGrid, currentRoof, args);
    }

    /// <summary>
    /// Planetary map path: propagate roof state down through the z-map network.
    /// </summary>
    private void OnMapTileChanged(
        EntityUid mapUid,
        MapGridComponent currentMapGrid,
        RoofComponent currentRoof,
        CEZMapComponent zLevelMapComp,
        TileChangedEvent args)
    {
        Dictionary<Vector2i, bool> roofMap = new();
        foreach (var change in args.Changes)
        {
            var tileDef = (ContentTileDefinition)TilDefMan[change.NewTile.TypeId];
            var roovedAbove = Roof.IsRooved((mapUid, currentMapGrid, currentRoof), change.GridIndices);
            roofMap.Add(change.GridIndices, roovedAbove || !tileDef.Transparent);
        }

        var mapsBelow = ZLevel.GetAllMapsBelow((mapUid, zLevelMapComp));
        if (mapsBelow.Count == 0)
            return;

        foreach (var mapBelow in mapsBelow)
        {
            if (!GridQuery.TryComp(mapBelow, out var mapGridBelow))
                continue;

            var roofBelow = EnsureComp<RoofComponent>(mapBelow);

            foreach (var (indices, rooved) in roofMap)
            {
                Roof.SetRoof((mapBelow, mapGridBelow, roofBelow), indices, rooved);

                if (Map.TryGetTile(mapGridBelow, indices, out var tile) && !tile.IsEmpty)
                    roofMap[indices] = true;
            }
        }
    }

    /// <summary>
    /// Space grid path: propagate roof state to sibling grids in the z-grid network using world tile coords.
    /// </summary>
    private void OnGridTileChanged(
        EntityUid gridUid,
        MapGridComponent currentMapGrid,
        RoofComponent currentRoof,
        TileChangedEvent args)
    {
        if (!ZLevel.TryGetGridNetwork(gridUid, out var network))
            return;

        Dictionary<Vector2i, bool> worldRoofMap = new();
        foreach (var change in args.Changes)
        {
            var worldTile = ZLevel.GridTileToWorldTile(gridUid, currentMapGrid, change.GridIndices);
            var tileDef = (ContentTileDefinition)TilDefMan[change.NewTile.TypeId];
            var roovedAbove = Roof.IsRooved((gridUid, currentMapGrid, currentRoof), change.GridIndices);
            worldRoofMap[worldTile] = roovedAbove || !tileDef.Transparent;
        }

        foreach (var otherGrid in network.Comp.Grids)
        {
            if (otherGrid == gridUid)
                continue;

            if (!GridQuery.TryComp(otherGrid, out var otherMapGrid))
                continue;

            var otherRoof = EnsureComp<RoofComponent>(otherGrid);
            var enumerator = Map.GetAllTilesEnumerator(otherGrid, otherMapGrid);
            while (enumerator.MoveNext(out var tileRef))
            {
                var worldTile = ZLevel.GridTileToWorldTile(otherGrid, otherMapGrid, tileRef.Value.GridIndices);
                if (!worldRoofMap.TryGetValue(worldTile, out var rooved))
                    continue;

                Roof.SetRoof((otherGrid, otherMapGrid, otherRoof), tileRef.Value.GridIndices, rooved);
            }
        }
    }
}
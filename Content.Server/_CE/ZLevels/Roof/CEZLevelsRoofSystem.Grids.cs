/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.ZLevels.Roof;
using Content.Shared.Light.Components;
using Content.Shared.Maps;

namespace Content.Server._CE.ZLevels.Roof;

public sealed partial class CEZLevelsRoofSystem
{
    [Dependency] private EntityQuery<CEZGridComponent> _zgridQuery = default!;
    [Dependency] private EntityQuery<CEZGridNetworkComponent> _zGridNetworkQuery = default!;

    private void InitGrids()
    {
        SubscribeLocalEvent<CEZGridComponent, MapInitEvent>(OnZGridMapInit);

        SubscribeLocalEvent<CEZGridNetworkComponent, CEZLevelGridNetworkUpdatedEvent>(OnZGridNetworkUpdate);
    }

    private void OnZGridNetworkUpdate(Entity<CEZGridNetworkComponent> ent, ref CEZLevelGridNetworkUpdatedEvent args)
    {
        RecalculateGridRoofs(ent);
    }

    private void OnZGridMapInit(Entity<CEZGridComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<CEZLevelRoofComponent>(ent.Owner);
    }

    public void RecalculateGridRoofs(Entity<CEZGridNetworkComponent> network)
    {
        _roofMap.Clear();

        var sorted = network.Comp.Grids
            .Select(g => (Grid: g, Depth: ZLevel.TryGetGridZDepth(g)))
            .Where(x => x.Depth.HasValue)
            .OrderByDescending(x => x.Depth!.Value);

        foreach (var (gridUid, _) in sorted)
        {
            RemCompDeferred<ImplicitRoofComponent>(gridUid); //hack but that way we dont need edit vanilla code

            if (!GridQuery.TryComp(gridUid, out var grid))
                continue;
            var roofComp = EnsureComp<RoofComponent>(gridUid);
            var enumerator = Map.GetAllTilesEnumerator(gridUid, grid);

            while (enumerator.MoveNext(out var tileRef))
            {
                var worldTile = ZLevel.GridTileToWorldTile(gridUid, grid, tileRef.Value.GridIndices);

                Roof.SetRoof((gridUid, grid, roofComp),
                    tileRef.Value.GridIndices,
                    _roofMap.Contains(worldTile));

                var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Value.Tile.TypeId];
                if (!tileDef.Transparent)
                    _roofMap.Add(worldTile);
            }
        }
    }
}

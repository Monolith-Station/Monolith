/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Light.Components;
using Content.Shared._CE.ZLevels.Roof;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Roof;

public sealed partial class CEZLevelsRoofSystem
{
    private void InitMaps()
    {
        SubscribeLocalEvent<CEZMapNetworkComponent, CEZLevelMapNetworkUpdatedEvent>(OnMapNetworkUpdated);
        SubscribeLocalEvent<CEZLevelRoofComponent, ComponentStartup>(OnRoofStartup);
        SubscribeLocalEvent<CEZMapComponent, ComponentShutdown>(OnMapRemoved);
        SubscribeLocalEvent<CEZLevelRoofComponent, TileChangedEvent>(OnTileChanged);
    }

    private void OnMapNetworkUpdated(Entity<CEZMapNetworkComponent> ent, ref CEZLevelMapNetworkUpdatedEvent args)
    {
        RecalculateMapRoofs(ent);
    }

    private void OnRoofStartup(Entity<CEZLevelRoofComponent> ent, ref ComponentStartup args)
    {
        if (_levels.TryGetMapNetwork(ent, out var network))
            RecalculateMapRoofs(network);
    }

    private void OnMapRemoved(Entity<CEZMapComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp<RoofComponent>(ent, out var roof))
        {
            roof.ZLevelData.Clear();
            Dirty(ent, roof);
        }

        if (TryComp<CEZLevelRoofComponent>(ent, out var zRoof))
        {
            zRoof.WeatherData.Clear();
            Dirty(ent, zRoof);
        }
    }

    private void OnTileChanged(Entity<CEZLevelRoofComponent> ent, ref TileChangedEvent args)
    {
        if (args.Changes.Length == 0 || !TryComp<CEZMapComponent>(ent, out var level))
            return;

        var mapsBelow = _levels.GetAllMapsBelow((ent.Owner, level));
        if (mapsBelow.Count == 0)
            return;

        TryComp<RoofComponent>(ent, out var currentRoof);
        var covers = new (Vector2i Index, bool Shadow, bool Weather)[args.Changes.Length];
        for (var i = 0; i < covers.Length; i++)
        {
            var change = args.Changes[i];
            var weatherAbove = IsCovered(ent.Comp.WeatherData, change.GridIndices);
            covers[i] = (change.GridIndices,
                CastsShadow(change.NewTile) || currentRoof != null && IsCovered(currentRoof.ZLevelData, change.GridIndices),
                BlocksWeather(change.NewTile) || weatherAbove);
        }

        foreach (var map in mapsBelow)
        {
            if (!TryComp<MapGridComponent>(map, out var grid))
                continue;

            var roof = EnsureComp<RoofComponent>(map);
            TryComp<CEZLevelRoofComponent>(map, out var zRoof);
            var shadowChanged = false;
            var weatherChanged = false;
            foreach (ref var cover in covers.AsSpan())
            {
                shadowChanged |= SetCover(roof.ZLevelData, cover.Index, cover.Shadow);
                if (zRoof != null)
                    weatherChanged |= SetCover(zRoof.WeatherData, cover.Index, cover.Weather);

                if (!_map.TryGetTile(grid, cover.Index, out var tile))
                    continue;

                cover.Shadow |= CastsShadow(tile);
                cover.Weather |= BlocksWeather(tile);
            }

            if (shadowChanged)
                Dirty(map, roof);
            if (weatherChanged && zRoof != null)
                Dirty(map, zRoof);
        }
    }

    private void RecalculateMapRoofs(Entity<CEZMapNetworkComponent> network)
    {
        var shadows = new Dictionary<Vector2i, ulong>();
        var weather = new Dictionary<Vector2i, ulong>();
        foreach (var (_, uid) in network.Comp.ZLevels.OrderByDescending(kv => kv.Key))
        {
            if (uid is not { } map || !TryComp<MapGridComponent>(map, out var grid))
                continue;

            var roof = EnsureComp<RoofComponent>(map);
            roof.ZLevelData = new(shadows);
            Dirty(map, roof);

            if (TryComp<CEZLevelRoofComponent>(map, out var zRoof))
            {
                zRoof.WeatherData = new(weather);
                Dirty(map, zRoof);
            }

            var tiles = _map.GetAllTilesEnumerator(map, grid);
            while (tiles.MoveNext(out var tileRef))
            {
                if (CastsShadow(tileRef.Value.Tile))
                    SetCover(shadows, tileRef.Value.GridIndices, true);
                if (BlocksWeather(tileRef.Value.Tile))
                    SetCover(weather, tileRef.Value.GridIndices, true);
            }
        }
    }
}

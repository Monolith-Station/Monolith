/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem : CESharedZLevelsSystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private StationSystem _station = default!;

    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitView();
        InitializeTransit();

        SubscribeLocalEvent<CEStationZLevelsComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<CEStationZLevelsComponent> ent, ref StationPostInitEvent args)
    {
        if (ent.Comp.MapsAbove.Count == 0 && ent.Comp.MapsBelow.Count == 0)
            return;

        var stationName = MetaData(ent).EntityName;
        var stationNetwork = CreateMapNetwork(ent.Comp.ZLevelsComponentOverrides);

        ent.Comp.ZNetworkEntity = stationNetwork;

        _meta.SetEntityName(ent.Comp.ZNetworkEntity.Value, $"Station z-Network: {stationName}");

        var mainGrid = _station.GetLargestGrid(ent.Owner);
        if (mainGrid is null)
            throw new Exception("Station has no grids to base z-levels off of!");

        var mainMapEnt = Transform(mainGrid.Value).MapUid
            ?? throw new Exception("Station main grid has no parent map entity!");

        var dict = new Dictionary<EntityUid, int>()
        {
            { mainMapEnt, 0 }
        };

        // Loading maps below first
        var depth = ent.Comp.MapsBelow.Count * -1;
        foreach (var mapBelow in ent.Comp.MapsBelow)
        {
            if (!_mapLoader.TryLoadMap(mapBelow, out var mapEnt, out _))
            {
                Log.Error($"Failed to load map for Station zNetwork at depth {depth}!");
                continue;
            }

            Log.Info($"Created map {mapEnt.Value.Comp.MapId} for Station zNetwork at level {depth}");
            _map.InitializeMap(mapEnt.Value.Comp.MapId);
            _meta.SetEntityName(mapEnt.Value, $"{stationName} [{depth}]");
            if (_mapGridQuery.HasComp(mapEnt.Value)) //Adding zlevel map to station, if it is planetmap
                _station.AddGridToStation(ent, mapEnt.Value);
            dict.Add(mapEnt.Value, depth);
            depth++;
        }

        // Loading maps above next
        depth = 1;
        foreach (var mapAbove in ent.Comp.MapsAbove)
        {
            if (!_mapLoader.TryLoadMap(mapAbove, out var mapEnt, out _))
            {
                Log.Error($"Failed to load map for Station zNetwork at depth {depth}!");
                continue;
            }

            Log.Info($"Created map {mapEnt.Value.Comp.MapId} for Station zNetwork at level {depth}");
            _map.InitializeMap(mapEnt.Value.Comp.MapId);
            _meta.SetEntityName(mapEnt.Value, $"{stationName} [{depth}]");
            if (_mapGridQuery.HasComp(mapEnt.Value)) //Adding zlevel map to station, if it is planetmap
                _station.AddGridToStation(ent, mapEnt.Value);
            dict.Add(mapEnt.Value, depth);
            depth++;
        }

        TryAddMapsIntoNetwork(stationNetwork, dict);
    }

    /// <summary>
    /// Initializes all uninitialized maps in the z-network.
    /// </summary>
    [PublicAPI]
    public void InitializeZNetwork(Entity<CEZMapNetworkComponent> network)
    {
        foreach (var (_, mapUid) in network.Comp.ZLevels)
        {
            if (!TryComp<MapComponent>(mapUid, out var mapComp))
            {
                Log.Error($"Map entity {mapUid} does not have MapComponent.");
                continue;
            }

            if (!_map.MapExists(mapComp.MapId))
            {
                Log.Error($"Map with ID {mapComp.MapId} does not exist.");
                continue;
            }

            if (_map.IsInitialized(mapComp.MapId))
            {
                Log.Debug($"Map with ID {mapComp.MapId} is already initialized.");
                continue;
            }

            _map.InitializeMap(mapComp.MapId);
            Log.Info($"Map with ID {mapComp.MapId} initialized.");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateGridGravity(frameTime);
        UpdateView(frameTime);
        CleanupOrphanedTransitMaps();
    }
}

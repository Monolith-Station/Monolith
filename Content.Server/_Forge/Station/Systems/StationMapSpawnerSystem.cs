using Content.Server._Forge.Station.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Forge.Station.Systems;

public sealed partial class StationMapSpawnerSystem : EntitySystem
{
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationMapSpawnerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StationMapSpawnerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<StationMapSpawnerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.GridPath is not {} path)
            return;

        ent.Comp.Map = LoadMap(path);
    }

    private EntityUid? LoadMap(ResPath path)
    {
        var map = _map.CreateMap(out var mapId, runMapInit: false);

        if (!_mapLoader.TryLoadGrid(mapId, path, out _))
        {
            Log.Error($"Failed to load map grid {path}!");
            Del(map);
            return null;
        }

        _map.InitializeMap(map);

        return map;
    }

    private void OnShutdown(Entity<StationMapSpawnerComponent> ent, ref ComponentShutdown args)
    {
        QueueDel(ent.Comp.Map);
    }
}

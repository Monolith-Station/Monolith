using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using Robust.Shared.EntitySerialization.Systems;;

namespace Content.Server._Mono.Spawning;

public sealed partial class GridSpawnerSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private override void Initialize()
    {
        SubscribeLocalEvent<GridSpawnerComponent, MapInitEvent>(OnInit);
    }

    private void OnInit(Entity<GridSpawnerComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);

        if (_loader.TryLoadGrid(xform.MapId, ent.Comp.Path, out var grid, offset: _transform.GetWorldPosition(xform)))
        {
            if (ent.Comp.NameGrid)
            {
                var name = ent.Comp.Path.FilenameWithoutExtension;
                _metadata.SetEntityName(grid.Value, name);
            }
        }
    }

    private void GridSpawns(EntityUid uid, GridSpawnComponent component)
    {
        if (!_cfg.GetCVar(CCVars.GridFill))
            return;

        if (!TryComp<StationDataComponent>(uid, out var data))
        {
            return;
        }

        var targetGrid = _station.GetLargestGrid(data);

        if (targetGrid == null)
            return;

        // Spawn on a dummy map and try to FTL if possible, otherwise dump it.
        _mapSystem.CreateMap(out var mapId);

        foreach (var group in component.Groups.Values)
        {
            var count = _random.Next(group.MinCount, group.MaxCount + 1);

            for (var i = 0; i < count; i++)
            {
                EntityUid spawned;

                switch (group)
                {
                    case DungeonSpawnGroup dungeon:
                        if (!TryDungeonSpawn(targetGrid.Value, dungeon, out spawned))
                            continue;

                        break;
                    case GridSpawnGroup grid:
                        if (!TryGridSpawn(targetGrid.Value, uid, mapId, grid, out spawned))
                            continue;

                        break;
                    default:
                        throw new NotImplementedException();
                }

                if (_protoManager.TryIndex(group.NameDataset, out var dataset))
                {
                    _metadata.SetEntityName(spawned, _salvage.GetFTLName(dataset, _random.Next()));
                }

                if (group.Hide)
                {
                    var iffComp = EnsureComp<IFFComponent>(spawned);
                    iffComp.Flags |= IFFFlags.HideLabel;
                    Dirty(spawned, iffComp);
                }

                if (group.StationGrid)
                {
                    _station.AddGridToStation(uid, spawned);
                }

                EntityManager.AddComponents(spawned, group.AddComponents);
            }
        }

        _mapSystem.DeleteMap(mapId);
    }

    private void OnGridFillMapInit(EntityUid uid, GridFillComponent component, MapInitEvent args)
    {
        if (!_cfg.GetCVar(CCVars.GridFill))
            return;

        if (!TryComp<DockingComponent>(uid, out var dock) ||
            !TryComp(uid, out TransformComponent? xform) ||
            xform.GridUid == null)
        {
            return;
        }

        // Spawn on a dummy map and try to dock if possible, otherwise dump it.
        _mapSystem.CreateMap(out var mapId);
        var valid = false;

        if (_loader.TryLoadGrid(mapId, component.Path, out var grid))
        {
            var escape = GetSingleDock(grid.Value);

            if (escape != null)
            {
                var config = _dockSystem.GetDockingConfig(grid.Value, xform.GridUid.Value, escape.Value.Entity, escape.Value.Component, uid, dock);

                if (config != null)
                {
                    var shuttleXform = Transform(grid.Value);
                    FTLDock((grid.Value, shuttleXform), config);

                    if (TryComp<StationMemberComponent>(xform.GridUid, out var stationMember))
                    {
                        _station.AddGridToStation(stationMember.Station, grid.Value);
                    }

                    valid = true;
                }
            }

            foreach (var compReg in component.AddComponents.Values)
            {
                var compType = compReg.Component.GetType();

                if (HasComp(grid.Value, compType))
                    continue;

                var comp = Factory.GetComponent(compType);
                AddComp(grid.Value, comp, true);
            }
        }

        if (!valid)
        {
            Log.Error($"Error loading gridfill dock for {ToPrettyString(uid)} / {component.Path}");
        }

        _mapSystem.DeleteMap(mapId);
    }

    private (EntityUid Entity, DockingComponent Component)? GetSingleDock(EntityUid uid)
    {
        var dockQuery = GetEntityQuery<DockingComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        var xform = xformQuery.GetComponent(uid);

        var rator = xform.ChildEnumerator;

        while (rator.MoveNext(out var child))
        {
            if (!dockQuery.TryGetComponent(child, out var dock))
                continue;

            return (child, dock);
        }

        return null;
    }
}

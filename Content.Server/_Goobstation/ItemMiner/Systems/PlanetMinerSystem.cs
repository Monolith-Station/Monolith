using Content.Server.Salvage.Expeditions;
using Content.Shared._Goobstation.ItemMiner;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Goobstation.ItemMiner;

public sealed class PlanetMinerSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<SalvageExpeditionComponent> _expedQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlanetMinerComponent, ItemMinerCheckEvent>(OnCheck);

        _mapQuery = GetEntityQuery<MapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _expedQuery = GetEntityQuery<SalvageExpeditionComponent>();
    }

    private void OnCheck(Entity<PlanetMinerComponent> ent, ref ItemMinerCheckEvent args)
    {
        if (args.Cancelled) // already cancelled
            return;

        var xform = Transform(ent);
        var mapUid = xform.MapUid;
        var gridUid = xform.GridUid;

        // check if we're on a planet
        if (!_gridQuery.TryComp(mapUid, out var mapGrid) || gridUid == null)
        {
            args.Cancelled = true;
            return;
        }

        if (mapUid != gridUid // if we're not on the planet surface
            && (ent.Comp.RequireGround // but have to be on the surface
                || !_map.GetTileRef((gridUid.Value, _gridQuery.GetComponent(gridUid.Value)), xform.Coordinates).IsSpace(_tileDef))) // or aren't on lattice
        {
            args.Cancelled = true; // then abort
            return;
        }

        if (ent.Comp.RequireExpedition)
            args.Cancelled |= !_expedQuery.HasComp(mapUid);
    }
}

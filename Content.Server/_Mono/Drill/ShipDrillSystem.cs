using System.Numerics;
using System.Transactions;
using Content.Server.Gatherable;
using Content.Server.Power.EntitySystems;
using Content.Shared.Decals;
using Robust.Shared.Map;

namespace Content.Server._Mono.Drill;

public partial class ShipDrillSystem : EntitySystem
{

    [Dependency] private EntityLookupSystem _look = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private SharedDecalSystem _decal = default!;
    [Dependency] private GatherableSystem _gather = default!;
    public override void Initialize()
    {
        InitializeGatherDrill();
    }

    private HashSet<EntityUid> _ents = new();

    private float _updateCooldown = 0.25f;
    private TimeSpan _updateTimer = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        if (_updateTimer < TimeSpan.FromSeconds(_updateCooldown))
        {
            _updateTimer += TimeSpan.FromSeconds(frameTime);
            return;
        }

        var eQe = EntityQueryEnumerator<ShipDrillComponent>();

        while (eQe.MoveNext(out var uid, out var comp))
        {
            if (!this.IsPowered(uid, EntityManager))
                continue;

            var coords = _xform.GetMapCoordinates(uid);
            var dGrid = Transform(uid).GridUid;

            if (!dGrid.HasValue)
                continue;

            var iX = comp.DrillWidth / 2 ;
            var iY = comp.DrillLength / 2 ;
            for (var x = -iX; x <= iX; x++)
            {
                for (var y = -iY; y <= iY; y++)
                {
                    ProcessTile(uid, dGrid.Value, comp, x, y, coords);
                }
            }
        }
        _updateTimer -= TimeSpan.FromSeconds(_updateCooldown);
    }

    private void ProcessTile(EntityUid uid, EntityUid drillGrid, ShipDrillComponent comp, float x, float y, MapCoordinates coords)
    {

        var oX = x + comp.DrillOffsetX;
        var oY = y + comp.DrillOffsetY;

        var angle = _xform.GetWorldRotation(uid).Theta;

        var nX = oX * Math.Cos(angle) - oY * Math.Sin(angle);
        var nY = oX * Math.Sin(angle) + oY * Math.Cos(angle);

        var nCoords = coords.Offset((float) nX, (float) nY);

        if (!_mapManager.TryFindGridAt(nCoords, out var grid, out var gridComp))
            return;

        if (grid == drillGrid)
            return;

        _ents.Clear();
        _look.GetEntitiesInRange(nCoords.MapId, nCoords.Position, EntityLookupSystem.LookupEpsilon, _ents, LookupFlags.Static);

        foreach (var ent in _ents)
        {
            comp.DrillType?.Drill(ent, this, EntityManager);
        }

        if (_ents.Count <= 0)
        {
            var tileRef = _map.GetTileRef(grid, gridComp, nCoords);
            var tileDef = _tileDef[tileRef.Tile.TypeId];

            if (!comp.TileWhitelist.Contains(tileDef.ID))
                return;

            var decals = _decal.GetDecalsInRange(grid, coords.Position, 0.5f);
            foreach (var (id, _) in decals)
            {
                _decal.RemoveDecal(tileRef.GridUid, id);
            }

            _map.SetTile(grid, gridComp, tileRef.GridIndices, Tile.Empty);
        }
    }
}

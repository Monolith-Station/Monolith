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
    private float _updateTimer = 0f;

    public override void Update(float frameTime)
    {
        if (_updateTimer <_updateCooldown)
        {
            _updateTimer += frameTime;
            return;
        }
        _updateTimer -= _updateCooldown;

        var eQe = EntityQueryEnumerator<ShipDrillComponent>();

        while (eQe.MoveNext(out var uid, out var comp))
        {
            if (!this.IsPowered(uid, EntityManager))
                continue;

            var coords = _xform.GetMapCoordinates(uid);
            var dGrid = Transform(uid).GridUid;

            if (!dGrid.HasValue)
                continue;

            var dVec = comp.DrillSize / 2;

            for (var x = -dVec.X; x <= dVec.X; x++)
            {
                for (var y = -dVec.Y; y <= dVec.Y; y++)
                {
                    ProcessTile(uid, dGrid.Value, comp, x, y, coords);
                }
            }
        }
    }

    private void ProcessTile(EntityUid uid, EntityUid drillGrid, ShipDrillComponent comp, float x, float y, MapCoordinates coords)
    {
        var rVec = _xform.GetWorldRotation(uid).RotateVec(new Vector2(x, y) + comp.DrillOffsets);
        var nCoords = coords.Offset(rVec);

        if (!_mapManager.TryFindGridAt(nCoords, out var grid, out var gridComp))
            return;

        if (grid == drillGrid)
            return;

        _ents.Clear();
        _look.GetEntitiesInRange(nCoords.MapId, nCoords.Position, EntityLookupSystem.LookupEpsilon, _ents, LookupFlags.Static);

        if (comp.DrillType != null)
        {
            foreach (var ent in _ents)
            {
                comp.DrillType.Drill(ent, uid, this, EntityManager);
            }
        }

        if (_ents.Count <= 0)
        {
            var tileRef = _map.GetTileRef(grid, gridComp, nCoords);
            var tileDef = _tileDef[tileRef.Tile.TypeId];

            if (!comp.TileWhitelist.Contains(tileDef.ID) && comp.TileWhitelist != null)
                return;

            _map.SetTile(grid, gridComp, tileRef.GridIndices, Tile.Empty);
        }
    }
}

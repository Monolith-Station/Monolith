using System.Linq;
using System.Numerics;
using System.Transactions;
using Content.Server.Gatherable;
using Content.Server.Power.EntitySystems;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Server._Mono.Drill;

public partial class ShipDrillSystem : EntitySystem
{

    [Dependency] private EntityLookupSystem _look = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private SharedDecalSystem _decal = default!;
    [Dependency] private GatherableSystem _gather = default!;

    private HashSet<EntityUid> _ents = new();
    private HashSet<TileRef> _nonEmptyTiles = new();

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
            var angle = _xform.GetWorldRotation(uid);
            var dGrid = Transform(uid).GridUid;

            if (!dGrid.HasValue)
                continue;

            var dVec = comp.DrillSize / 2;
            var tVec = new Vector2(0.25f, 0.25f);

            var worldBox = new Box2Rotated(
                new Box2(coords.Offset(-dVec + comp.DrillOffsets).Position, coords.Offset(dVec + comp.DrillOffsets).Position),
                angle,
                coords.Position);

            /// I dont want to do this but RT lookups being so evil is fucking insane.
            /// Apparently tile lookup is bigger than entity lookup for literally 0 reason.
            var tileWorldBox = new Box2Rotated(
                new Box2(coords.Offset(-dVec + tVec + comp.DrillOffsets).Position, coords.Offset(dVec - tVec + comp.DrillOffsets).Position),
                angle,
                coords.Position);

            var grids = _mapManager.FindGridsIntersecting(_xform.GetMapId(dGrid.Value), worldBox);

            foreach (var grid in grids)
            {
                if (grid.Owner == dGrid)
                    continue;

                var tiles = _map.GetTilesIntersecting(grid.Owner, grid, tileWorldBox);
                _look.GetEntitiesIntersecting(grid.Owner, worldBox, _ents, LookupFlags.Static);

                foreach (var ent in _ents)
                {
                    comp.DrillType?.Drill(ent, uid, this, EntityManager);
                    var tileRef = _map.GetTileRef(grid.Owner, grid, Transform(ent).Coordinates);
                    _nonEmptyTiles.Add(tileRef);
                }

                var tilesToDelete = tiles.ToList();
                tilesToDelete.RemoveAll(tile => _nonEmptyTiles.Contains(tile));

                foreach (var tileRef in tilesToDelete)
                {
                    var tileDef = _tileDef[tileRef.Tile.TypeId];

                    if (comp.TileWhitelist != null && !comp.TileWhitelist.Contains(tileDef.ID))
                        continue;

                    _map.SetTile(grid.Owner, grid, tileRef.GridIndices, Tile.Empty);
                }

                _ents.Clear();
                _nonEmptyTiles.Clear();
            }
        }
    }
}


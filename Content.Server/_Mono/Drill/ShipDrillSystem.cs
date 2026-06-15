using System.Numerics;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared._Mono.Drill;
using Content.Shared.Decals;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Server._Mono.Drill;

public sealed partial class ShipDrillSystem : SharedShipDrillSystem
{

    [Dependency] private EntityLookupSystem _look = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private SharedDecalSystem _decal = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private GatherableSystem _gather = default!;
    public override void Initialize()
    {
        _gatherQuery = GetEntityQuery<GatherableComponent>();
    }

    private HashSet<EntityUid> _ents = new();
    private EntityQuery<GatherableComponent> _gatherQuery;

    public override void Update(float frameTime)
    {
        var eQe = EntityQueryEnumerator<ShipDrillComponent>();

        while (eQe.MoveNext(out var uid, out var comp))
        {
            var coords = _xform.GetMapCoordinates(uid);
            var dGrid = Transform(uid).GridUid;

            var iX = comp.DrillWidth%2==0 ? (int) MathF.Ceiling(comp.DrillWidth / 2f) - 1 :  comp.DrillWidth / 2 - 1;
            var iY = comp.DrillLength%2==0 ? (int) MathF.Ceiling(comp.DrillLength / 2f) - 1 :  comp.DrillLength / 2 - 1;

            var eX = comp.DrillWidth%2==0 ? (int) MathF.Ceiling(comp.DrillWidth / 2f) - 1 :  comp.DrillWidth / 2;
            var eY = comp.DrillLength % 2 == 0 ? (int) MathF.Ceiling(comp.DrillLength / 2f) - 1 : comp.DrillLength / 2;

            for (var x = -iX; x <= eX; x++)
            {
                for (var y = -iY; y <= eY; y++)
                {
                    var oX = (x + comp.DrillOffsetX);
                    var oY = (y + comp.DrillOffsetY);

                    var sin = (float) Math.Sin(_xform.GetWorldRotation(uid).Theta);
                    var cos = (float)Math.Cos(_xform.GetWorldRotation(uid).Theta);

                    var nX = oX * cos - oY * sin;
                    var nY = oX * sin + oY * cos;

                    var nCoords = coords.Offset(nX, nY);

                    if (!_mapManager.TryFindGridAt(nCoords, out var grid, out var gridComp))
                        continue;

                    if (grid == dGrid)
                        continue;

                    _ents.Clear();
                    _look.GetEntitiesInRange(nCoords.MapId, nCoords.Position, EntityLookupSystem.LookupEpsilon, _ents, LookupFlags.Static);

                    foreach (var ent in _ents)
                    {
                        if (_gatherQuery.TryComp(ent, out var gather))
                            _gather.Gather(ent, uid, gather);
                    }

                    if (_ents.Count <= 0)
                    {
                        var tileRef = _map.GetTileRef(grid, gridComp, nCoords);
                        _map.SetTile(grid, gridComp, tileRef.GridIndices, Tile.Empty);
                    }
                }
            }
        }
    }
}

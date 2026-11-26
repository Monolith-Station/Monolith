using System.Numerics;
using Content.Server._CE.ZLevels.PVS;
using Content.Shared._CE.Planets.Shields;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.Planets.Shields;

public sealed partial class CEShieldBeamSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private IMapManager _mapManager = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(CESharedShieldGeneratorSystem));
        SubscribeLocalEvent<CEShieldBeamComponent, ComponentStartup>(OnBeamStartup);
    }

    private void OnBeamStartup(Entity<CEShieldBeamComponent> ent, ref ComponentStartup args) =>
        EnsureComp<CEPvsOverrideComponent>(ent);

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CEShieldBeamComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beam, out var xform))
        {
            if (beam.Source || EntityManager.IsQueuedForDeletion(uid))
                continue;

            var position = _transform.GetWorldPosition(xform);
            var bounds = Box2.CenteredAround(position, new Vector2(beam.Radius * 2));
            var grids = new List<Entity<MapGridComponent>>();
            _mapManager.FindGridsIntersecting(xform.MapID, bounds, ref grids);
            foreach (var grid in grids)
            {
                var tiles = new List<(Vector2i, Tile)>();
                foreach (var tile in _maps.GetTilesIntersecting(grid.Owner, grid.Comp, bounds))
                    tiles.Add((tile.GridIndices, Tile.Empty));
                if (tiles.Count > 0)
                    _maps.SetTiles(grid.Owner, grid.Comp, tiles);
            }
        }
    }
}

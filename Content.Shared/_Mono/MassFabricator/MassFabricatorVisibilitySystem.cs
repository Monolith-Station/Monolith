using System.Numerics;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Shared._Mono.MassFabricator;

public partial class MassFabricatorVisibilitySystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;

    private readonly HashSet<Entity<OccluderComponent>> _occluders = new();
    private readonly Dictionary<Vector2i, bool> _opaque = new();
    private EntityQuery<OccluderComponent> _occluderQuery;

    public override void Initialize()
    {
        base.Initialize();
        _occluderQuery = GetEntityQuery<OccluderComponent>();
    }

    public bool CanSee(EntityUid player, Entity<BroadphaseComponent, MapGridComponent> grid, EntityCoordinates coords)
    {
        if (!coords.IsValid(EntityManager) || Transform(player).MapID != Transform(grid).MapID ||
            _transforms.GetGrid(coords) != grid.Owner)
            return false;

        _opaque.Clear();
        var index = _maps.CoordinatesToTile(grid.Owner, grid.Comp2, coords);
        var origin = GetOrigin(player, grid);
        return IsVisible(grid, index, origin, _config.GetCVar(CVars.NetMaxUpdateRange) * 0.5f);
    }

    public void GetView(EntityUid player, Entity<BroadphaseComponent, MapGridComponent> grid,
        Box2Rotated worldBounds, HashSet<Vector2i> visible)
    {
        var radius = _config.GetCVar(CVars.NetMaxUpdateRange) * 0.5f;
        visible.Clear();
        _opaque.Clear();

        var matrix = _transforms.GetInvWorldMatrix(grid.Owner);
        var origin = GetOrigin(player, grid);
        var bounds = matrix.TransformBox(worldBounds);
        var tiles = _maps.GetLocalTilesEnumerator(grid.Owner, grid.Comp2, bounds, ignoreEmpty: false);
        while (tiles.MoveNext(out var tile))
            if (IsVisible(grid, tile.GridIndices, origin, radius))
                visible.Add(tile.GridIndices);
    }

    private Vector2 GetOrigin(EntityUid player, Entity<BroadphaseComponent, MapGridComponent> grid)
    {
        return Vector2.Transform(_transforms.GetWorldPosition(player), _transforms.GetInvWorldMatrix(grid.Owner)) /
            grid.Comp2.TileSize;
    }

    private bool IsVisible(Entity<BroadphaseComponent, MapGridComponent> grid, Vector2i index,
        Vector2 origin, float radius)
    {
        var target = new Vector2(index.X + 0.5f, index.Y + 0.5f);
        var distance = Vector2.Distance(origin, target);
        if (distance > radius)
            return false;

        var steps = Math.Max(1, (int) MathF.Ceiling(distance * 4));
        for (var step = 0; step < steps; step++)
        {
            var point = Vector2.Lerp(origin, target, (float) step / steps);
            var intermediate = new Vector2i((int) MathF.Floor(point.X), (int) MathF.Floor(point.Y));
            if (intermediate != index && IsOpaque(grid, intermediate))
                return false;
        }

        return true;
    }

    private bool IsOpaque(Entity<BroadphaseComponent, MapGridComponent> grid, Vector2i tile)
    {
        if (_opaque.TryGetValue(tile, out var opaque))
            return opaque;

        _occluders.Clear();
        var bounds = _lookup.GetLocalBounds(tile, grid.Comp2.TileSize).Enlarged(-0.05f);
        _lookup.GetLocalEntitiesIntersecting((grid.Owner, grid.Comp1), bounds, _occluders,
            query: _occluderQuery, flags: LookupFlags.Static | LookupFlags.Approximate);
        foreach (var occluder in _occluders)
            if (occluder.Comp.Enabled)
                return _opaque[tile] = true;

        return _opaque[tile] = false;
    }
}

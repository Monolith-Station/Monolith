using System.Numerics;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class MassFabricatorVisionSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;

    private readonly HashSet<Entity<OccluderComponent>> _occluders = new();
    private readonly Dictionary<Vector2i, bool> _opaque = new();
    private EntityQuery<OccluderComponent> _occluderQuery;

    public void GetView(EntityUid player, Entity<BroadphaseComponent, MapGridComponent> grid,
        Box2Rotated worldBounds, HashSet<Vector2i> visible, float radius = 7.5f)
    {
        visible.Clear();
        _opaque.Clear();

        var matrix = _transforms.GetInvWorldMatrix(grid.Owner);
        var origin = Vector2.Transform(_transforms.GetWorldPosition(player), matrix) / grid.Comp2.TileSize;
        var bounds = matrix.TransformBox(worldBounds);
        var tiles = _maps.GetLocalTilesEnumerator(grid.Owner, grid.Comp2, bounds, ignoreEmpty: false);
        while (tiles.MoveNext(out var tile))
        {
            var index = tile.GridIndices;
            var target = new Vector2(index.X + 0.5f, index.Y + 0.5f);
            var distance = Vector2.Distance(origin, target);
            if (distance > radius)
                continue;

            var steps = Math.Max(1, (int) MathF.Ceiling(distance * 4));
            var blocked = false;
            for (var step = 0; step < steps; step++)
            {
                var point = Vector2.Lerp(origin, target, (float) step / steps);
                var intermediate = new Vector2i((int) MathF.Floor(point.X), (int) MathF.Floor(point.Y));
                if (intermediate != index && IsOpaque(grid, intermediate))
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
                visible.Add(index);
        }
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
        {
            if (occluder.Comp.Enabled)
                return _opaque[tile] = true;
        }

        return _opaque[tile] = false;
    }

    public override void Initialize()
    {
        base.Initialize();
        _occluderQuery = GetEntityQuery<OccluderComponent>();
    }
}

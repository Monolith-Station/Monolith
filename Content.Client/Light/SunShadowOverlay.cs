using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Prototypes;

namespace Content.Client.Light;

public sealed partial class SunShadowOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> MixShader = "Mix";
    private static readonly Vector2[] TileCastPoints =
    [
        new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f),
    ];
    private const int MaxRoofShadowBatchVertices = 1536;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _mapSys;
    private readonly SharedTransformSystem _xformSys;

    private readonly HashSet<Entity<SunShadowCastComponent>> _shadows = new();
    private readonly HashSet<Vector2i> _roofedTiles = new();
    private readonly HashSet<Vector2i> _roofEntityTiles = new();
    private readonly List<Vector2> _roofShadowVertices = new();

    private readonly OverlayResourceCache<CachedResources> _resources = new();

    public SunShadowOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xformSys = _entManager.System<SharedTransformSystem>();
        _mapSys = _entManager.System<SharedMapSystem>();
        _lookup = _entManager.System<EntityLookupSystem>();
        ZIndex = AfterLightTargetOverlay.ContentZIndex + 1;
    }

    private List<Entity<MapGridComponent>> _grids = new();

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var eye = viewport.Eye;

        if (eye == null)
            return;

        _grids.Clear();
        _mapSys.FindGridsIntersecting(args.MapId,
            args.WorldBounds.Enlarged(SunShadowComponent.MaxLength),
            ref _grids);

        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldBounds = args.WorldBounds;
        var targetSize = viewport.LightRenderTarget.Size;

        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());

        if (res.Target?.Size != targetSize)
        {
            res.Target = _clyde
                .CreateRenderTarget(targetSize,
                    new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                    name: "sun-shadow-target");

            if (res.BlurTarget?.Size != targetSize)
            {
                res.BlurTarget = _clyde
                    .CreateRenderTarget(targetSize, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "sun-shadow-blur");
            }
        }

        var lightScale = viewport.LightRenderTarget.Size / (Vector2)viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);

        foreach (var grid in _grids)
        {
            if (!_entManager.TryGetComponent(grid.Owner, out SunShadowComponent? sun))
            {
                continue;
            }

            var direction = sun.Direction;
            var alpha = Math.Clamp(sun.Alpha, 0f, 1f);

            // Nowhere to cast to so ignore it.
            if (direction.Equals(Vector2.Zero) || alpha == 0f)
                continue;

            // Feature todo: dynamic shadows for mobs and trees. Also ideally remove the fake tree shadows.
            // TODO: Jittering still not quite perfect

            var expandedBounds = worldBounds.Enlarged(direction.Length() + 0.01f);
            _shadows.Clear();

            // Draw shadow polys to stencil
            args.WorldHandle.RenderInRenderTarget(res.Target,
                () =>
                {
                    var invMatrix =
                        res.Target.GetWorldToLocalMatrix(eye, scale);
                    var indices = new Vector2[PhysicsConstants.MaxPolygonVertices * 2];

                    _roofedTiles.Clear();
                    if (_entManager.TryGetComponent(grid.Owner, out RoofComponent? roof))
                        DrawRoofShadows(worldHandle, grid, roof, expandedBounds, direction, invMatrix, indices);

                    // Go through shadows in range.

                    // For each one we:
                    // - Get the original vertices.
                    // - Extrapolate these along the sun direction.
                    // - Combine the above into 1 single polygon to draw.

                    // Note that this is range-limited for accuracy; if you set it too high it will clip through walls or other undesirable entities.
                    // This is probably not noticeable most of the time but if you want something "accurate" you'll want to code a solution.
                    // Ideally the CPU would have its own shadow-map copy that we could just ray-cast each vert into though
                    // You might need to batch verts or the likes as this could get expensive.
                    _lookup.GetEntitiesIntersecting(mapId, expandedBounds, _shadows);

                    foreach (var ent in _shadows)
                    {
                        var xform = _entManager.GetComponent<TransformComponent>(ent.Owner);
                        if (xform.GridUid == grid.Owner &&
                            _mapSys.TryGetTileRef(grid.Owner, grid.Comp, xform.Coordinates, out var tile) &&
                            _roofedTiles.Contains(tile.GridIndices))
                            continue;

                        var (worldPos, worldRot) = _xformSys.GetWorldPositionRotation(xform);
                        worldHandle.SetTransform(Matrix3x2.CreateTranslation(worldPos) * invMatrix);
                        if (ent.Comp.UseFixtureBounds &&
                            DrawFixtureShadows(ent.Owner, worldHandle, indices, worldRot, direction))
                            continue;

                        ent.Comp.Points.CopyTo(indices, 0);
                        DrawCaster(worldHandle, indices, ent.Comp.Points.Length, worldRot, direction);
                    }
                },
                Color.Transparent);

            // Slightly blur it just to avoid aliasing issues on the later viewport-wide blur.
            _clyde.BlurRenderTarget(viewport, res.Target, res.BlurTarget!, eye, 1f);

            // Draw stencil (see roofoverlay).
            args.WorldHandle.RenderInRenderTarget(viewport.LightRenderTarget,
                () =>
                {
                    var invMatrix =
                        viewport.LightRenderTarget.GetWorldToLocalMatrix(eye, scale);
                    worldHandle.SetTransform(invMatrix);

                    var maskShader = _protoManager.Index(MixShader).Instance();
                    worldHandle.UseShader(maskShader);

                    worldHandle.DrawTextureRect(res.Target.Texture, worldBounds, Color.Black.WithAlpha(alpha));
                }, null);
        }
    }

    private bool DrawFixtureShadows(EntityUid uid, DrawingHandleWorld handle, Vector2[] points, Angle rotation, Vector2 direction)
    {
        if (!_entManager.TryGetComponent(uid, out FixturesComponent? fixtures))
            return false;

        var drew = false;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            switch (fixture.Shape)
            {
                case PolygonShape { VertexCount: >= 3 and <= PhysicsConstants.MaxPolygonVertices } polygon:
                    var vertices = polygon.Vertices;
                    vertices.CopyTo(points, 0);
                    DrawCaster(handle, points, vertices.Length, rotation, direction);
                    break;
                case PhysShapeAabb aabb:
                    var box = aabb.LocalBounds;
                    points[0] = box.BottomLeft;
                    points[1] = box.BottomRight;
                    points[2] = box.TopRight;
                    points[3] = box.TopLeft;
                    DrawCaster(handle, points, 4, rotation, direction);
                    break;
                default:
                    continue;
            }

            drew = true;
        }

        return drew;
    }

    private static void DrawCaster(DrawingHandleWorld handle, Vector2[] points, int count, Angle rotation, Vector2 direction)
    {
        for (var i = 0; i < count; i++)
        {
            points[i] = rotation.RotateVec(points[i]);
            points[count + i] = points[i] + direction;
        }

        var hull = PhysicsHull.ComputePoints(points, count * 2);
        if (hull.Length >= 3)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, hull, Color.White);
    }

    private void DrawRoofShadows(DrawingHandleWorld handle, Entity<MapGridComponent> grid, RoofComponent roof,
        Box2Rotated bounds, Vector2 direction, Matrix3x2 worldToTarget, Vector2[] points)
    {
        var size = grid.Comp.TileSize;
        var gridMatrix = _xformSys.GetWorldMatrix(grid.Owner);
        for (var i = 0; i < TileCastPoints.Length; i++)
        {
            points[i] = Vector2.TransformNormal(TileCastPoints[i] * size, gridMatrix);
            points[i + TileCastPoints.Length] = points[i] + direction;
        }

        var hull = PhysicsHull.ComputePoints(points, TileCastPoints.Length * 2);
        if (hull.Length < 3)
            return;

        _roofEntityTiles.Clear();
        var roofEntities = _entManager.EntityQueryEnumerator<IsRoofComponent, TransformComponent>();
        while (roofEntities.MoveNext(out var uid, out var isRoof, out var xform))
        {
            if (isRoof.Enabled && xform.GridUid == grid.Owner &&
                (!_entManager.TryGetComponent(uid, out SunShadowCastComponent? caster) || !caster.UseFixtureBounds))
                _roofEntityTiles.Add(_mapSys.TileIndicesFor(grid, xform.Coordinates));
        }

        var tiles = _mapSys.GetTilesEnumerator(grid.Owner, grid, bounds);
        _roofShadowVertices.Clear();
        handle.SetTransform(worldToTarget);
        while (tiles.MoveNext(out var tile))
        {
            var index = tile.GridIndices;
            if (!SharedRoofSystem.HasRoofFlag(roof, index) && !_roofEntityTiles.Contains(index))
                continue;

            _roofedTiles.Add(index);
            if (_roofShadowVertices.Count + (hull.Length - 2) * 3 > MaxRoofShadowBatchVertices)
            {
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _roofShadowVertices, Color.White);
                _roofShadowVertices.Clear();
            }

            var center = Vector2.Transform((new Vector2(index.X, index.Y) + new Vector2(0.5f)) * size, gridMatrix);
            for (var i = 1; i < hull.Length - 1; i++)
            {
                _roofShadowVertices.Add(center + hull[0]);
                _roofShadowVertices.Add(center + hull[i]);
                _roofShadowVertices.Add(center + hull[i + 1]);
            }
        }

        if (_roofShadowVertices.Count > 0)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _roofShadowVertices, Color.White);
        _roofShadowVertices.Clear();
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();

        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? BlurTarget;
        public IRenderTexture? Target;

        public void Dispose()
        {
            BlurTarget?.Dispose();
            Target?.Dispose();
        }
    }
}

using System.Numerics;
using Content.Client._CE.ZLevels.Core.Overlays;
using Content.Client.Graphics;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Light;

public sealed partial class SunShadowOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShadowBlitShader = "unshaded";
    private static readonly Vector2[] TileCastPoints =
    [
        new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f),
    ];
    private const int MaxShadowBatchVertices = 1536;
    private const int EstimatedTilesPerChunk = 16 * 16;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _mapSys;
    private readonly SharedTransformSystem _xformSys;

    private readonly HashSet<Entity<SunShadowCastComponent>> _shadows = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _roofedTiles = new();
    private const int HullCacheCapacity = 512;
    private readonly Vector2[] _points = new Vector2[PhysicsConstants.MaxPolygonVertices * 2];
    private readonly Vector2[] _cachedHullPoints = new Vector2[HullCacheCapacity * PhysicsConstants.MaxPolygonVertices];
    private readonly Dictionary<(Vector2 A, Vector2 B, Vector2 C, Vector2 D, int Count, Vector2 Cast), (int Start, int Count)> _hullCache = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _roofEntityTiles = new();
    private uint? _roofIndexFrame;
    private readonly List<Vector2> _shadowVertices = new(MaxShadowBatchVertices);
    private readonly List<TileRef> _visibleShadowTiles = new();

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
    private List<Entity<MapGridComponent>> _projectedGrids = new();

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

        var mapSun = _entManager.GetComponentOrNull<SunShadowComponent>(args.MapUid);
        var receiver = args.MapUid;
        var passes = mapSun != null ? 1 : _grids.Count;
        for (var i = 0; i < passes; i++)
        {
            var sun = mapSun;
            if (sun == null && !_entManager.TryGetComponent(_grids[i].Owner, out sun))
                continue;

            var direction = sun.Direction;
            var alpha = Math.Clamp(sun.Alpha, 0f, 1f);

            // Nowhere to cast to so ignore it.
            if (direction.Equals(Vector2.Zero) || alpha == 0f)
                continue;

            // Feature todo: dynamic shadows for mobs and trees. Also ideally remove the fake tree shadows.
            // TODO: Jittering still not quite perfect

            var expandedBounds = worldBounds.Enlarged(direction.Length() + 0.01f);
            _shadows.Clear();

            args.WorldHandle.RenderInRenderTarget(res.Target,
                () =>
                {
                    var invMatrix =
                        res.Target.GetWorldToLocalMatrix(eye, scale);
                    var indices = _points;
                    worldHandle.UseShader(_protoManager.Index(ShadowBlitShader).Instance());

                    _roofedTiles.Clear();
                    var roofEnd = mapSun != null ? _grids.Count : i + 1;
                    for (var j = i; j < roofEnd; j++)
                    {
                        var grid = _grids[j];
                        if (grid.Comp.ChunkCount != 0 && _entManager.TryGetComponent(grid.Owner, out RoofComponent? roof))
                        {
                            CollectShadowTiles(grid, expandedBounds);
                            if (_visibleShadowTiles.Count != 0)
                                DrawRoofShadows(worldHandle, grid, roof, direction, invMatrix, indices);
                        }
                    }

                    DrawCasters(worldHandle, mapId, expandedBounds, direction, invMatrix, indices);
                    if (mapSun != null)
                        DrawOverheadShadows(worldHandle, receiver, worldBounds, direction, invMatrix, indices);
                    worldHandle.UseShader(null);
                },
                Color.Transparent);

            // Slightly blur it just to avoid aliasing issues on the later viewport-wide blur.
            _clyde.BlurRenderTarget(viewport, res.Target, res.BlurTarget!, eye, 1f);

            var screen = args.RenderHandle.DrawingHandleScreen;
            screen.RenderInRenderTarget(viewport.LightRenderTarget,
                () =>
                {
                    screen.SetTransform(Matrix3x2.Identity);
                    screen.UseShader(_protoManager.Index(ShadowBlitShader).Instance());
                    screen.DrawTextureRect(res.Target.Texture, new UIBox2(Vector2.Zero, targetSize), Color.Black.WithAlpha(alpha));
                    screen.UseShader(null);
                }, null);
        }
    }

    private void DrawOverheadShadows(DrawingHandleWorld handle, EntityUid receiver, Box2Rotated bounds,
        Vector2 direction, Matrix3x2 worldToMask, Vector2[] points)
    {
        if (!_overlays.TryGetOverlay<CEZLevelShadowOverlay>(out var zShadows))
            return;

        foreach (var plane in zShadows.GetShadowPlanes(receiver))
        {
            if (!_entManager.TryGetComponent(plane.Map, out MapComponent? map))
                continue;

            var offset = -direction * MathF.Max(0f, plane.Gap);
            var sourceBounds = new Box2Rotated(bounds.Box.Translated(offset), bounds.Rotation, bounds.Origin + offset)
                .Enlarged(direction.Length() * (plane.Transit ? 1f : 2f) + 0.01f);
            _projectedGrids.Clear();
            _mapSys.FindGridsIntersecting(map.MapId, sourceBounds, ref _projectedGrids, approx: true);
            _roofedTiles.Clear();
            foreach (var grid in _projectedGrids)
            {
                if (grid.Comp.ChunkCount == 0)
                    continue;

                var height = zShadows.GetCasterHeight(grid.Owner, plane);
                var start = MathF.Max(0f, height);
                var length = 1f + MathF.Min(0f, height);
                var footprint = height > 0.01f;
                var roof = plane.Map != receiver && length > 0f
                    ? _entManager.GetComponentOrNull<RoofComponent>(grid.Owner)
                    : null;
                if (!footprint && roof == null)
                    continue;

                CollectShadowTiles(grid, sourceBounds);
                if (_visibleShadowTiles.Count == 0)
                    continue;

                var projection = Matrix3x2.CreateTranslation(direction * start) * worldToMask;
                if (footprint)
                    DrawGridFootprint(handle, grid, projection);

                if (roof != null)
                    DrawRoofShadows(handle, grid, roof, direction * length, projection, points);
            }

            if (plane.Map != receiver)
                DrawCasters(handle, map.MapId, sourceBounds, direction, worldToMask, points, zShadows, plane);
        }
    }

    private void CollectShadowTiles(Entity<MapGridComponent> grid, Box2Rotated bounds)
    {
        _visibleShadowTiles.Clear();
        var local = _xformSys.GetInvWorldMatrix(grid.Owner).TransformBox(bounds);
        var left = (int) MathF.Floor(local.Left);
        var bottom = (int) MathF.Floor(local.Bottom);
        var right = (int) MathF.Ceiling(local.Right);
        var top = (int) MathF.Ceiling(local.Top);
        if (right <= left || top <= bottom)
            return;

        // Prefer stored chunks when the rectangle is much larger than the grid. Either path selects the same tiles.
        if (((long) right - left) * ((long) top - bottom) > (long) grid.Comp.ChunkCount * EstimatedTilesPerChunk)
        {
            var allTiles = _mapSys.GetAllTilesEnumerator(grid.Owner, grid.Comp);
            while (allTiles.MoveNext(out var tile))
            {
                var index = tile.Value.GridIndices;
                if (index.X >= left && index.X < right && index.Y >= bottom && index.Y < top)
                    _visibleShadowTiles.Add(tile.Value);
            }
        }
        else
        {
            var tiles = _mapSys.GetLocalTilesEnumerator(grid.Owner, grid.Comp, local);
            while (tiles.MoveNext(out var tile))
                _visibleShadowTiles.Add(tile);
        }
    }

    private void DrawGridFootprint(DrawingHandleWorld handle, Entity<MapGridComponent> grid, Matrix3x2 projection)
    {
        var size = grid.Comp.TileSize;
        handle.SetTransform(_xformSys.GetWorldMatrix(grid.Owner) * projection);
        foreach (var tile in _visibleShadowTiles)
        {
            if (_tileDefinitions[tile.Tile.TypeId] is not ContentTileDefinition { Transparent: false } definition)
                continue;

            var origin = new Vector2(tile.GridIndices.X, tile.GridIndices.Y);
            var vertices = definition.Vertices;
            if (vertices.Count < 3 || vertices.Count > _points.Length)
            {
                FlushShadowVertices(handle);
                handle.DrawRect(Box2.FromDimensions(origin * size, new Vector2(size)), Color.White);
                continue;
            }

            if (_shadowVertices.Count + (vertices.Count - 2) * 3 > MaxShadowBatchVertices)
                FlushShadowVertices(handle);

            var first = (origin + vertices[0]) * size;
            var previous = (origin + vertices[1]) * size;
            for (var i = 2; i < vertices.Count; i++)
            {
                var next = (origin + vertices[i]) * size;
                _shadowVertices.Add(first);
                _shadowVertices.Add(previous);
                _shadowVertices.Add(next);
                previous = next;
            }
        }

        FlushShadowVertices(handle);
    }

    private void DrawCasters(DrawingHandleWorld handle, MapId map, Box2Rotated bounds, Vector2 direction,
        Matrix3x2 worldToMask, Vector2[] points, CEZLevelShadowOverlay? zShadows = null,
        CEZLevelShadowOverlay.ShadowPlane plane = default)
    {
        _shadows.Clear();
        _lookup.GetEntitiesIntersecting(map, bounds, _shadows);
        foreach (var ent in _shadows)
        {
            var xform = _entManager.GetComponent<TransformComponent>(ent.Owner);
            if (_roofedTiles.Count > 0 && xform.GridUid is { } gridUid &&
                _entManager.TryGetComponent(gridUid, out MapGridComponent? grid) &&
                _mapSys.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tile) &&
                _roofedTiles.Contains((gridUid, tile.GridIndices)))
                continue;

            var height = zShadows?.GetCasterHeight(ent.Owner, plane) ?? 0f;
            var start = MathF.Max(0f, height);
            var length = 1f + MathF.Min(0f, height);
            if (length <= 0f)
                continue;

            var (worldPos, worldRot) = _xformSys.GetWorldPositionRotation(xform);
            handle.SetTransform(Matrix3x2.CreateTranslation(worldPos + direction * start) * worldToMask);
            var cast = direction * length;
            if (ent.Comp.UseFixtureBounds && DrawFixtureShadows(ent.Owner, handle, points, worldRot, cast))
                continue;

            ent.Comp.Points.CopyTo(points, 0);
            DrawCaster(handle, points, ent.Comp.Points.Length, worldRot, cast);
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

    private void DrawCaster(DrawingHandleWorld handle, Vector2[] points, int count, Angle rotation, Vector2 direction)
    {
        for (var i = 0; i < count; i++)
            points[i] = rotation.RotateVec(points[i]);

        var (start, length) = GetCasterHull(points, count, direction);
        var hull = _cachedHullPoints.AsSpan(start, length);
        if (hull.Length >= 3)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, hull, Color.White);
    }

    private void DrawRoofShadows(DrawingHandleWorld handle, Entity<MapGridComponent> grid, RoofComponent roof,
        Vector2 direction, Matrix3x2 worldToTarget, Vector2[] points)
    {
        var size = grid.Comp.TileSize;
        var gridMatrix = _xformSys.GetWorldMatrix(grid.Owner);
        for (var i = 0; i < TileCastPoints.Length; i++)
            points[i] = Vector2.TransformNormal(TileCastPoints[i] * size, gridMatrix);

        var (start, length) = GetCasterHull(points, TileCastPoints.Length, direction);
        var hull = _cachedHullPoints.AsSpan(start, length);
        if (hull.Length < 3)
            return;

        UpdateRoofEntityIndex();

        handle.SetTransform(worldToTarget);
        foreach (var tile in _visibleShadowTiles)
        {
            var index = tile.GridIndices;
            if (!SharedRoofSystem.HasRoofFlag(roof, index) && !_roofEntityTiles.Contains((grid.Owner, index)))
                continue;

            _roofedTiles.Add((grid.Owner, index));
            if (_shadowVertices.Count + (hull.Length - 2) * 3 > MaxShadowBatchVertices)
                FlushShadowVertices(handle);

            var center = Vector2.Transform((new Vector2(index.X, index.Y) + new Vector2(0.5f)) * size, gridMatrix);
            for (var i = 1; i < hull.Length - 1; i++)
            {
                _shadowVertices.Add(center + hull[0]);
                _shadowVertices.Add(center + hull[i]);
                _shadowVertices.Add(center + hull[i + 1]);
            }
        }

        FlushShadowVertices(handle);
    }

    private void FlushShadowVertices(DrawingHandleWorld handle)
    {
        if (_shadowVertices.Count == 0)
            return;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _shadowVertices, Color.White);
        _shadowVertices.Clear();
    }

    private (int Start, int Count) GetCasterHull(Vector2[] points, int count, Vector2 direction)
    {
        if (count is < 3 or > PhysicsConstants.MaxPolygonVertices / 2)
            return default;

        var key = (points[0], points[1], points[2], count == 4 ? points[3] : default, count, direction);
        if (_hullCache.TryGetValue(key, out var cached))
            return cached;

        if (_hullCache.Count == HullCacheCapacity)
            _hullCache.Clear();

        for (var i = 0; i < count; i++)
            points[count + i] = points[i] + direction;

        var hull = PhysicsHull.ComputePoints(points, count * 2);
        var start = _hullCache.Count * PhysicsConstants.MaxPolygonVertices;
        hull.CopyTo(_cachedHullPoints.AsSpan(start, hull.Length));
        _hullCache.Add(key, (start, hull.Length));
        return (start, hull.Length);
    }

    private void UpdateRoofEntityIndex()
    {
        if (_roofIndexFrame == _timing.CurFrame)
            return;

        _roofIndexFrame = _timing.CurFrame;
        _roofEntityTiles.Clear();
        var roofs = _entManager.EntityQueryEnumerator<IsRoofComponent, TransformComponent>();
        while (roofs.MoveNext(out var uid, out var roof, out var xform))
        {
            if (roof.Enabled && xform.GridUid is { } gridUid &&
                _entManager.TryGetComponent(gridUid, out MapGridComponent? grid) &&
                (!_entManager.TryGetComponent(uid, out SunShadowCastComponent? caster) || !caster.UseFixtureBounds))
                _roofEntityTiles.Add((gridUid, _mapSys.TileIndicesFor(gridUid, grid, xform.Coordinates)));
        }
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

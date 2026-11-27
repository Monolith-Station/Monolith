using System.Numerics;
using Content.Client.Graphics;
using Content.Client.Viewport;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Prototypes;

namespace Content.Client.Light;

/// <summary>
/// Applies ambient-occlusion to the viewport.
/// </summary>
public sealed partial class AmbientOcclusionOverlay : Overlay
{
    // Clyde's blur radius is in UV space, even when the AO target is downsampled.
    private const float BlurMultiplier = 14f;
    private const int MaxBatchVertices = 1536;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfgManager = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly List<Vector2> _vertices = new(MaxBatchVertices);
    private readonly Vector2[] _occluderVertices = new Vector2[PhysicsConstants.MaxPolygonVertices];
    private List<Entity<MapGridComponent>> _grids = new();

    public AmbientOcclusionOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = AfterLightTargetOverlay.ContentZIndex + 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not { } eye || eye is ScalingViewport.ZEye { PlanetTransitBackdrop: true })
            return;

        /*
         * tl;dr
         * - we draw each occluder's polygon to an AO source texture.
         * - we blur this.
         * - We apply it to the viewport.
         *
         * We do this while ignoring lighting because it will wash out the actual effect.
         * In 3D ambient occlusion is more complicated due top having to calculate normals but in 2D
         * we don't have a concept of depth / corners necessarily.
         */

        var viewport = args.Viewport;
        var mapId = args.MapId;
        var worldBounds = args.WorldBounds;
        var worldHandle = args.WorldHandle;
        var color = Color.FromHex(_cfgManager.GetCVar(CCVars.AmbientOcclusionColor));
        var distance = _cfgManager.GetCVar(CCVars.AmbientOcclusionDistance);
        var expansion = distance / EyeManager.PixelsPerMeter;
        var resolution = Math.Clamp(_cfgManager.GetCVar(CCVars.AmbientOcclusionResolutionScale), 0.1f, 1f);
        var target = viewport.RenderTarget;
        var aoSize = new Vector2i(Math.Max(1, (int) MathF.Ceiling(target.Size.X * resolution)),
            Math.Max(1, (int) MathF.Ceiling(target.Size.Y * resolution)));
        var lightScale = aoSize / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);
        var cameraSize = eye.Zoom.Y * viewport.Size.Y / viewport.RenderScale.Y / EyeManager.PixelsPerMeter;
        var expandedBounds = worldBounds.Enlarged(expansion + BlurMultiplier / cameraSize);
        var maps = _entManager.System<SharedMapSystem>();
        var lookups = _entManager.System<EntityLookupSystem>();
        var query = _entManager.System<OccluderSystem>();
        var xformSystem = _entManager.System<SharedTransformSystem>();
        var turfSystem = _entManager.System<TurfSystem>();
        var invMatrix = args.Viewport.GetWorldToLocalMatrix();

        var cache = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (cache.Target?.Texture.Size != aoSize)
        {
            cache.Target?.Dispose();
            cache.Blur?.Dispose();
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb);
            cache.Target = _clyde.CreateRenderTarget(aoSize, format, name: "ambient-occlusion-target");
            cache.Blur = _clyde.CreateRenderTarget(aoSize, format, name: "ambient-occlusion-blur-target");
        }

        if (cache.Stencil?.Texture.Size != target.Size)
        {
            cache.Stencil?.Dispose();
            cache.Stencil = _clyde.CreateRenderTarget(target.Size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "ambient-occlusion-stencil-target");
        }

        // Draw the texture data to the texture.
        args.WorldHandle.RenderInRenderTarget(cache.Target,
            () =>
            {
                worldHandle.UseShader(_proto.Index<ShaderPrototype>("unshaded").Instance());
                worldHandle.SetTransform(Matrix3x2.Identity);
                var worldToTarget = cache.Target.GetWorldToLocalMatrix(eye, scale);
                foreach (var entry in query.QueryAabb(mapId, expandedBounds))
                {
                    var matrix = xformSystem.GetWorldMatrix(entry.Transform) * worldToTarget;
                    if (TryAppendFixtureOccluders(worldHandle, entry.Uid, matrix, expansion))
                        continue;

                    AppendOccluder(worldHandle, entry.Component.Polygon, matrix, expansion);
                }

                FlushPolygons(worldHandle);
            }, Color.Transparent);

        _clyde.BlurRenderTarget(viewport, cache.Target, cache.Blur!, eye, BlurMultiplier);

        // Need to do stencilling after blur as it will nuke it.
        // Draw stencil for the grid so we don't draw in space.
        args.WorldHandle.RenderInRenderTarget(cache.Stencil,
            () =>
            {
                // Don't want lighting affecting it.
                worldHandle.UseShader(_proto.Index<ShaderPrototype>("unshaded").Instance());

                _grids.Clear();
                maps.FindGridsIntersecting(mapId, worldBounds, ref _grids);
                foreach (var grid in _grids)
                {
                    var transform = xformSystem.GetWorldMatrix(grid.Owner);
                    var worldToTextureMatrix = Matrix3x2.Multiply(transform, invMatrix);
                    var tiles = maps.GetTilesEnumerator(grid.Owner, grid, worldBounds);
                    worldHandle.SetTransform(worldToTextureMatrix);
                    while (tiles.MoveNext(out var tileRef))
                    {
                        if (turfSystem.IsSpace(tileRef))
                            continue;

                        var bounds = lookups.GetLocalBounds(tileRef, grid.Comp.TileSize);
                        worldHandle.DrawRect(bounds, Color.White);
                    }
                }

            }, Color.Transparent);

        // Draw the stencil texture to depth buffer.
        worldHandle.UseShader(_proto.Index<ShaderPrototype>("StencilMask").Instance());
        worldHandle.DrawTextureRect(cache.Stencil.Texture, worldBounds);

        // Draw the Blurred AO texture finally.
        worldHandle.UseShader(_proto.Index<ShaderPrototype>("StencilEqualDraw").Instance());
        worldHandle.DrawTextureRect(cache.Target.Texture, worldBounds, color);

        worldHandle.UseShader(_proto.Index<ShaderPrototype>("StencilClear").Instance());
        worldHandle.DrawRect(worldBounds, Color.White);

        args.WorldHandle.SetTransform(Matrix3x2.Identity);
        args.WorldHandle.UseShader(null);
    }

    private bool TryAppendFixtureOccluders(DrawingHandleWorld handle, EntityUid uid, Matrix3x2 transform, float expansion)
    {
        if (!_entManager.TryGetComponent(uid, out FixturesComponent? fixtures))
            return false;

        var drew = false;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard || (fixture.CollisionLayer & (int) CollisionGroup.Opaque) == 0 ||
                fixture.Shape is not PolygonShape { VertexCount: >= 3 } polygon)
                continue;

            var vertices = polygon.Vertices;
            var center = polygon.Centroid;
            Vector2 Project(Vector2 vertex)
            {
                var offset = vertex - center;
                if (offset.LengthSquared() > 0f)
                    vertex += Vector2.Normalize(offset) * expansion;
                return Vector2.Transform(vertex, transform);
            }

            var first = Project(vertices[0]);
            var previous = Project(vertices[1]);
            for (var i = 2; i < vertices.Length; i++)
            {
                var current = Project(vertices[i]);
                AppendTriangle(handle, first, previous, current);
                previous = current;
            }

            drew = true;
        }

        return drew;
    }

    private void AppendOccluder(DrawingHandleWorld handle, ReadOnlySpan<Vector2> polygon, Matrix3x2 transform, float expansion)
    {
        if (polygon.Length < 3)
            return;

        var center = Vector2.Zero;
        foreach (var vertex in polygon)
            center += vertex;
        center /= polygon.Length;

        var vertices = _occluderVertices;
        for (var i = 0; i < polygon.Length; i++)
        {
            var offset = polygon[i] - center;
            var extent = MathF.Max(MathF.Abs(offset.X), MathF.Abs(offset.Y));
            var vertex = extent > 0 ? polygon[i] + offset * (expansion / extent) : polygon[i];
            vertices[i] = Vector2.Transform(vertex, transform);
        }

        for (var i = 1; i < polygon.Length - 1; i++)
            AppendTriangle(handle, vertices[0], vertices[i], vertices[i + 1]);
    }

    private void AppendTriangle(DrawingHandleWorld handle, Vector2 a, Vector2 b, Vector2 c)
    {
        if (_vertices.Count + 3 > MaxBatchVertices)
            FlushPolygons(handle);

        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
    }

    private void FlushPolygons(DrawingHandleWorld handle)
    {
        if (_vertices.Count == 0)
            return;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _vertices, Color.White);
        _vertices.Clear();
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Target;
        public IRenderTexture? Blur;
        public IRenderTexture? Stencil;

        public void Dispose()
        {
            Target?.Dispose();
            Blur?.Dispose();
            Stencil?.Dispose();
        }
    }
}

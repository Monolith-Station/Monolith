using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Light;

/// <summary>
/// Applies ambient-occlusion to the viewport.
/// </summary>
public sealed partial class AmbientOcclusionOverlay : Overlay
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfgManager = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    private IRenderTexture? _aoTarget;
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly Vector2[] _occluderVertices = new Vector2[PhysicsConstants.MaxPolygonVertices];
    private readonly Vector2[] _occluderTriangles = new Vector2[3 * 1024];

    // Couldn't figure out a way to avoid this so if you can then please do.
    private IRenderTexture? _aoStencilTarget;

    public AmbientOcclusionOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = AfterLightTargetOverlay.ContentZIndex + 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        /*
         * tl;dr
         * - we draw a black square on each "ambient occlusion" entity.
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
        var expansion = _cfgManager.GetCVar(CCVars.AmbientOcclusionDistance) / EyeManager.PixelsPerMeter;
        //var color = Color.Red;
        var target = viewport.RenderTarget;
        var lightScale = target.Size / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);
        var maps = _entManager.System<SharedMapSystem>();
        var lookups = _entManager.System<EntityLookupSystem>();
        var query = _entManager.System<OccluderSystem>();
        var xformSystem = _entManager.System<SharedTransformSystem>();
        var turfSystem = _entManager.System<TurfSystem>();
        var invMatrix = args.Viewport.GetWorldToLocalMatrix();

        if (_aoTarget?.Texture.Size != target.Size)
        {
            _aoTarget?.Dispose();
            _aoTarget = _clyde.CreateRenderTarget(target.Size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "ambient-occlusion-target");
        }

        if (_aoStencilTarget?.Texture.Size != target.Size)
        {
            _aoStencilTarget?.Dispose();
            _aoStencilTarget = _clyde.CreateRenderTarget(target.Size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "ambient-occlusion-stencil-target");
        }

        // Draw the texture data to the texture.
        args.WorldHandle.RenderInRenderTarget(_aoTarget,
            () =>
            {
                worldHandle.UseShader(_proto.Index<ShaderPrototype>("unshaded").Instance());
                worldHandle.SetTransform(Matrix3x2.Identity);
                var invMatrix = _aoTarget.GetWorldToLocalMatrix(viewport.Eye!, scale);
                var triangleVertexCount = 0;

                foreach (var entry in query.QueryAabb(mapId, worldBounds))
                {
                    DebugTools.Assert(entry.Component.Enabled);
                    var matrix = xformSystem.GetWorldMatrix(entry.Transform);
                    var localMatrix = Matrix3x2.Multiply(matrix, invMatrix);

                    var polygon = entry.Component.Polygon;
                    var center = Vector2.Zero;
                    foreach (var vertex in polygon)
                        center += vertex;
                    center /= polygon.Length;

                    var vertices = _occluderVertices.AsSpan(0, polygon.Length);
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        var offset = polygon[i] - center;
                        var extent = MathF.Max(MathF.Abs(offset.X), MathF.Abs(offset.Y));
                        var vertex = extent > 0 ? polygon[i] + offset * (expansion / extent) : polygon[i];
                        vertices[i] = Vector2.Transform(vertex, localMatrix);
                    }

                    if (triangleVertexCount + 3 * (vertices.Length - 2) > _occluderTriangles.Length)
                    {
                        DrawOccluderTriangles(worldHandle, triangleVertexCount);
                        triangleVertexCount = 0;
                    }

                    for (var i = 1; i < vertices.Length - 1; i++)
                    {
                        _occluderTriangles[triangleVertexCount++] = vertices[0];
                        _occluderTriangles[triangleVertexCount++] = vertices[i];
                        _occluderTriangles[triangleVertexCount++] = vertices[i + 1];
                    }
                }

                DrawOccluderTriangles(worldHandle, triangleVertexCount);
            }, Color.Transparent);

        _clyde.BlurRenderTarget(viewport, _aoTarget, _aoTarget, viewport.Eye!, 14f);

        // Need to do stencilling after blur as it will nuke it.
        // Draw stencil for the grid so we don't draw in space.
        args.WorldHandle.RenderInRenderTarget(_aoStencilTarget,
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
        worldHandle.DrawTextureRect(_aoStencilTarget!.Texture, worldBounds);

        // Draw the Blurred AO texture finally.
        worldHandle.UseShader(_proto.Index<ShaderPrototype>("StencilEqualDraw").Instance());
        worldHandle.DrawTextureRect(_aoTarget!.Texture, worldBounds, color);

        worldHandle.UseShader(_proto.Index<ShaderPrototype>("StencilClear").Instance());
        worldHandle.DrawRect(worldBounds, Color.White);

        args.WorldHandle.SetTransform(Matrix3x2.Identity);
        args.WorldHandle.UseShader(null);
    }

    private void DrawOccluderTriangles(DrawingHandleWorld handle, int vertexCount)
    {
        if (vertexCount > 0)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _occluderTriangles.AsSpan(0, vertexCount), Color.White);
    }
}

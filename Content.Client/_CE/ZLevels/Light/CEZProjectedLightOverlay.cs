using System.Numerics;
using Content.Client.Light;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using static Content.Client._CE.ZLevels.Light.CEZLightProjectionSystem;

namespace Content.Client._CE.ZLevels.Light;

internal sealed partial class CEZProjectedLightOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> LightShader = "CEZProjectedLight";
    private static readonly ProtoId<ShaderPrototype> CopyShader = "CEZLightBufferCopy";
    private static readonly ProtoId<ShaderPrototype> CloudShader = "CEZLightCloudMask";
    private const int MaskSize = 128;
    private const float WallHeight = 1f;
    private const float CloudThickness = 0.25f;
    private static readonly ushort[][] WallFaces = BuildWallFaces();

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;

    private readonly CEZLightProjectionSystem _lights;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;
    private readonly EntityLookupSystem _lookup;
    private readonly OccluderSystem _occluders;
    private readonly EntityQuery<MapComponent> _mapQuery;
    private readonly EntityQuery<CEZCloudLayerComponent> _cloudQuery;
    private readonly List<ShaderInstance> _cloudShaders = new();
    private readonly List<ProjectedLight> _visible = new();
    private readonly List<Entity<OccluderComponent, TransformComponent>> _walls = new();
    private readonly Vector2[] _wallCorners = new Vector2[PhysicsConstants.MaxPolygonVertices * 2];
    private List<Entity<MapGridComponent>> _grids = new();
    private IRenderTexture? _lightBuffer;
    private IRenderTexture? _mask;
    private ShaderInstance? _shader;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public CEZProjectedLightOverlay(IEntityManager entities, CEZLightProjectionSystem lights)
    {
        IoCManager.InjectDependencies(this);
        _lights = lights;
        _map = entities.System<SharedMapSystem>();
        _transform = entities.System<SharedTransformSystem>();
        _lookup = entities.System<EntityLookupSystem>();
        _occluders = entities.System<OccluderSystem>();
        _mapQuery = entities.GetEntityQuery<MapComponent>();
        _cloudQuery = entities.GetEntityQuery<CEZCloudLayerComponent>();
        ZIndex = AfterLightTargetOverlay.ContentZIndex + 2;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        if (viewport.Eye is not { } eye)
            return;

        _visible.Clear();
        foreach (var light in _lights.ProjectedLights)
        {
            if (light.Target.Map == args.MapUid && new Circle(light.Position, light.Reach).Intersects(args.WorldAABB))
                _visible.Add(light);
        }

        if (_visible.Count == 0)
            return;

        var target = viewport.LightRenderTarget;
        if (_lightBuffer?.Size != target.Size)
        {
            _lightBuffer?.Dispose();
            _lightBuffer = _clyde.CreateLightRenderTarget(target.Size, "Z-light buffer", depthStencil: false);
        }

        var buffer = _lightBuffer;
        var mask = _mask ??= _clyde.CreateRenderTarget(new Vector2i(MaskSize, MaskSize),
            RenderTargetColorFormat.Rgba8, new TextureSampleParameters { Filter = true }, "Z-light aperture");
        var shader = _shader ??= _prototypes.Index(LightShader).InstanceUnique();
        var copy = _prototypes.Index(CopyShader).Instance();
        var world = args.WorldHandle;
        var screen = args.RenderHandle.DrawingHandleScreen;
        var screenBounds = new UIBox2(Vector2.Zero, target.Size);
        var worldToBuffer = buffer.GetWorldToLocalMatrix(eye, viewport.RenderScale * (target.Size / (Vector2) viewport.Size));

        // Clyde's additive blend needs an opaque destination; copy the existing light buffer first.
        screen.RenderInRenderTarget(buffer, () =>
        {
            screen.UseShader(copy);
            screen.DrawTextureRect(target.Texture, screenBounds);
            screen.UseShader(null);

            foreach (var light in _visible)
            {
                world.RenderInRenderTarget(mask, () => DrawBlockers(world, light), Color.White);
                DrawLight(world, worldToBuffer, mask.Texture, shader, light);
            }
        }, null);

        screen.RenderInRenderTarget(target, () =>
        {
            screen.UseShader(copy);
            screen.DrawTextureRect(buffer.Texture, screenBounds);
            screen.UseShader(null);
        }, null);
    }

    private void DrawBlockers(DrawingHandleWorld handle, ProjectedLight light)
    {
        var span = light.Target.Altitude - light.EmitterAltitude;
        var cloudPass = 0;
        foreach (var plane in light.Planes)
        {
            var floor = plane.Altitude - light.EmitterAltitude;
            var ceiling = floor + WallHeight;
            if (span == 0f)
            {
                if (light.Source.CastShadows && floor <= 0f && ceiling > 0f)
                    DrawWallBlockers(handle, light, plane.Map, 0f, 1f);
                continue;
            }

            var floorFraction = floor / span;
            // Rays arriving from below must also clear the receiving floor.
            if (floorFraction is > 0f and < 1f || span > 0f && plane.Map == light.Target.Map)
                DrawFloorBlockers(handle, light, plane.Map, floorFraction);

            if (light.Source.CastShadows)
            {
                var ceilingFraction = ceiling / span;
                DrawWallBlockers(handle, light, plane.Map, MathF.Min(floorFraction, ceilingFraction),
                    MathF.Max(floorFraction, ceilingFraction));
            }

            if (_cloudQuery.HasComp(plane.Map) && DrawCloudMask(handle, light, floor, span, cloudPass))
                cloudPass++;
        }
    }

    private bool DrawCloudMask(DrawingHandleWorld handle, ProjectedLight light, float cloudTop, float span, int pass)
    {
        var lower = MathF.Max(MathF.Min(0f, span), cloudTop - CloudThickness);
        var upper = MathF.Min(MathF.Max(0f, span), cloudTop);
        if (upper <= lower)
            return false;

        // Each queued layer needs its own uniforms until this mask is flushed.
        if (pass == _cloudShaders.Count)
            _cloudShaders.Add(_prototypes.Index(CloudShader).InstanceUnique());

        var shader = _cloudShaders[pass];
        var midFraction = (lower + upper) / (2f * span);
        shader.SetParameter("cloudCenter", light.Position);
        shader.SetParameter("cloudReach", light.Reach * midFraction);
        shader.SetParameter("cloudOverlap", (upper - lower) / CloudThickness);

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(shader);
        handle.DrawRect(new Box2(0, 0, MaskSize, MaskSize), Color.White);
        handle.UseShader(null);
        return true;
    }

    private void DrawFloorBlockers(DrawingHandleWorld handle, ProjectedLight light, EntityUid map, float fraction)
    {
        var reach = light.Reach * fraction;
        var bounds = new Box2(light.Position - new Vector2(reach), light.Position + new Vector2(reach));
        var scale = MaskSize / (2f * reach);
        var matrix = Matrix3x2.CreateTranslation(-light.Position) *
                     Matrix3x2.CreateScale(scale, -scale) *
                     Matrix3x2.CreateTranslation(MaskSize / 2f, MaskSize / 2f);

        _grids.Clear();
        _map.FindGridsIntersecting(map, bounds, ref _grids, approx: true);
        foreach (var grid in _grids)
        {
            handle.SetTransform(_transform.GetWorldMatrix(grid.Owner) * matrix);
            var tiles = _map.GetTilesEnumerator(grid.Owner, grid.Comp, bounds);
            while (tiles.MoveNext(out var tile))
            {
                if (_tiles[tile.Tile.TypeId] is ContentTileDefinition { Transparent: false })
                    handle.DrawRect(_lookup.GetLocalBounds(tile, grid.Comp.TileSize), Color.Black);
            }
        }
    }

    private void DrawWallBlockers(DrawingHandleWorld handle, ProjectedLight light, EntityUid map, float start, float end)
    {
        // Clip the prism to the ray segment and keep the projection finite at the emitter.
        var near = MathF.Max(start, 0.0001f);
        var far = MathF.Min(end, 1f);
        if (near >= far || !_mapQuery.TryComp(map, out var mapComp))
            return;

        var offset = new Vector2(light.Reach * far);
        var bounds = new Box2(light.Position - offset, light.Position + offset);
        _walls.Clear();
        _occluders.QueryAabb(_walls, mapComp.MapId, bounds);

        var center = new Vector2(MaskSize / 2f);
        var scale = new Vector2(1f, -1f) * (MaskSize / (2f * light.Reach));
        var corners = _wallCorners;
        handle.SetTransform(Matrix3x2.Identity);
        foreach (var wall in _walls)
        {
            var polygon = wall.Comp1.Polygon;
            var count = polygon.Length;
            if (!wall.Comp1.Enabled || count < 3 || count > PhysicsConstants.MaxPolygonVertices)
                continue;

            var matrix = _transform.GetWorldMatrix(wall.Comp2);
            for (var i = 0; i < count; i++)
            {
                var delta = (Vector2.Transform(polygon[i], matrix) - light.Position) * scale;
                corners[i] = center + delta / near;
                corners[i + count] = center + delta / far;
            }

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, WallFaces[count], corners.AsSpan(0, count * 2), Color.Black);
        }
    }

    private static ushort[][] BuildWallFaces()
    {
        var faces = new ushort[PhysicsConstants.MaxPolygonVertices + 1][];
        for (var n = 3; n < faces.Length; n++)
        {
            var list = new List<ushort>((n - 2) * 6 + n * 6);
            for (var i = 1; i < n - 1; i++)
            {
                list.AddRange([0, (ushort) i, (ushort) (i + 1)]);
                list.AddRange([(ushort) n, (ushort) (n + i + 1), (ushort) (n + i)]);
            }

            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                list.AddRange([(ushort) i, (ushort) (n + i), (ushort) (n + j)]);
                list.AddRange([(ushort) i, (ushort) (n + j), (ushort) j]);
            }

            faces[n] = list.ToArray();
        }

        return faces;
    }

    private void DrawLight(DrawingHandleWorld handle, Matrix3x2 matrix, Texture mask, ShaderInstance shader, ProjectedLight light)
    {
        var source = light.Source;
        var beam = source.LightMask is { } maskId && _prototypes.TryIndex(maskId, out var lightMask)
            ? _resources.GetResource<TextureResource>(lightMask.MaskPath).Texture
            : Texture.White;
        var rotation = source.MaskAutoRotate ? source.Rotation + light.Rotation : source.Rotation;
        shader.SetParameter("beam", beam);
        shader.SetParameter("beamAxis", rotation.RotateVec(Vector2.UnitX));
        shader.SetParameter("lightColor", source.Color);
        shader.SetParameter("lightRange", source.Radius);
        shader.SetParameter("lightPower", source.Energy);
        shader.SetParameter("lightFalloff", source.Falloff);
        shader.SetParameter("lightCurveFactor", source.CurveFactor);
        shader.SetParameter("projectedRadius", light.Reach);
        shader.SetParameter("heightSquared", light.HeightSquared);

        var offset = new Vector2(light.Reach);
        handle.SetTransform(matrix);
        handle.UseShader(shader);
        handle.DrawTextureRect(mask, new Box2(light.Position - offset, light.Position + offset));
        handle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _lightBuffer?.Dispose();
        _mask?.Dispose();
        _shader?.Dispose();
        foreach (var shader in _cloudShaders)
            shader.Dispose();
        base.DisposeBehavior();
    }
}

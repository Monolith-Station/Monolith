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
using Robust.Shared.Timing;
using static Content.Client._CE.ZLevels.Light.CEZLightProjectionSystem;

namespace Content.Client._CE.ZLevels.Light;

internal sealed partial class CEZProjectedLightOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> LightShader = "CEZProjectedLight";
    private static readonly ProtoId<ShaderPrototype> CopyShader = "CEZLightBufferCopy";
    private static readonly ProtoId<ShaderPrototype> CloudShader = "CEZLightCloudMask";
    private const int MaskSize = 512;
    private const float WallHeight = 1f;
    private const float CloudThickness = 0.25f;
    private static readonly ushort[][] WallFaces = BuildWallFaces();

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IGameTiming _timing = default!;
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
    private readonly List<(Vector2 Position, float Altitude)> _emitters = new();
    private readonly Vector2[] _wallCorners = new Vector2[PhysicsConstants.MaxPolygonVertices * 2];
    private List<Entity<MapGridComponent>> _grids = new();

    private readonly Dictionary<(EntityUid Light, EntityUid Map), PlaneGeometry> _geometry = new();
    private readonly Stack<PlaneGeometry> _geometryPool = new();
    private uint _geometryFrame;

    // Changing these is performance expensive. If 6 isn't enough you may have an issue.
    private const int Samples = 6;
    private const float SourceRadius = 0.25f;
    private static readonly Vector3[] SampleOffsets = BuildSampleOffsets();

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
        _lights.CollectProjected(args.MapUid, _visible);
        var kept = 0;
        for (var i = 0; i < _visible.Count; i++)
        {
            var light = _visible[i];
            if (new Circle(light.Position, light.Reach).Intersects(args.WorldAABB) &&
                TargetCanReceive(light) && !FloorBlocksAll(light))
            {
                _visible[kept++] = light;
            }
        }

        _visible.RemoveRange(kept, _visible.Count - kept);
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

        screen.RenderInRenderTarget(buffer, () =>
        {
            screen.UseShader(copy);
            screen.DrawTextureRect(target.Texture, screenBounds);
            screen.UseShader(null);

            foreach (var light in _visible)
            {
                CollectEmitters(light);
                var share = 1f / _emitters.Count;
                var weight = Color.ToSrgb(new Color(share, share, share));
                foreach (var (emitter, altitude) in _emitters)
                {
                    world.RenderInRenderTarget(mask, () => DrawBlockers(world, light, emitter, altitude), Color.White);
                    DrawLight(world, worldToBuffer, mask.Texture, shader, light, weight);
                }
            }
        }, null);

        screen.RenderInRenderTarget(target, () =>
        {
            screen.UseShader(copy);
            screen.DrawTextureRect(buffer.Texture, screenBounds);
            screen.UseShader(null);
        }, null);
    }

    private void DrawBlockers(DrawingHandleWorld handle, ProjectedLight light, Vector2 emitter, float emitterAltitude)
    {
        var span = light.Target.Altitude - emitterAltitude;
        var low = MathF.Min(emitterAltitude, light.Target.Altitude);
        var high = MathF.Max(emitterAltitude, light.Target.Altitude);
        var cloudPass = 0;

        var planes = light.Planes;
        for (var i = FirstPlaneAbove(planes, low - WallHeight); i < planes.Count; i++)
        {
            var plane = planes[i];
            if (plane.Altitude > high + CloudThickness)
                break;

            var floor = plane.Altitude - emitterAltitude;
            var ceiling = floor + WallHeight;
            if (span == 0f)
            {
                if (light.Source.CastShadows && floor <= 0f && ceiling > 0f)
                    DrawWallBlockers(handle, light, emitter, GetGeometry(light, plane.Map), 0f, 1f);
                continue;
            }

            var floorFraction = floor / span;
            // Rays arriving from below must also clear the receiving floor.
            if (floorFraction is > 0f and < 1f || span > 0f && plane.Map == light.Target.Map)
                DrawFloorBlockers(handle, light, emitter, GetGeometry(light, plane.Map), floorFraction);

            if (light.Source.CastShadows)
            {
                var ceilingFraction = ceiling / span;
                DrawWallBlockers(handle, light, emitter, GetGeometry(light, plane.Map),
                    MathF.Min(floorFraction, ceilingFraction), MathF.Max(floorFraction, ceilingFraction));
            }

            if (_cloudQuery.HasComp(plane.Map) && DrawCloudMask(handle, light, emitter, floor, span, cloudPass))
                cloudPass++;
        }
    }

    private bool TargetCanReceive(ProjectedLight light)
    {
        var fromBelow = light.Target.Altitude > light.EmitterAltitude;
        var radius = light.Reach + SourceRadius + 0.5f;
        _grids.Clear();
        _map.FindGridsIntersecting(light.Target.Map, Box2.CenteredAround(light.Position, new Vector2(radius * 2f)), ref _grids, approx: true);
        foreach (var grid in _grids)
        {
            var tileSize = grid.Comp.TileSize;
            var local = Vector2.Transform(light.Position, _transform.GetInvWorldMatrix(grid.Owner)) / tileSize;
            var r = radius / tileSize;
            var min = (local - new Vector2(r)).Floored();
            var max = (local + new Vector2(r)).Floored();
            for (var y = min.Y; y <= max.Y; y++)
            for (var x = min.X; x <= max.X; x++)
            {
                var nearest = Vector2.Clamp(local, new Vector2(x, y), new Vector2(x + 1, y + 1));
                if ((nearest - local).LengthSquared() > r * r ||
                    !_map.TryGetTileRef(grid.Owner, grid.Comp, new Vector2i(x, y), out var tile) ||
                    tile.Tile.IsEmpty)
                {
                    continue;
                }

                if (!fromBelow || _tiles[tile.Tile.TypeId] is ContentTileDefinition { Transparent: true })
                    return true;
            }
        }

        return false;
    }

    private bool FloorBlocksAll(ProjectedLight light)
    {
        var span = light.Target.Altitude - light.EmitterAltitude;
        if (span == 0f)
            return false;

        var low = MathF.Min(light.EmitterAltitude, light.Target.Altitude);
        var high = MathF.Max(light.EmitterAltitude, light.Target.Altitude);
        var planes = light.Planes;
        for (var i = FirstPlaneAbove(planes, low); i < planes.Count && planes[i].Altitude <= high; i++)
        {
            var plane = planes[i];
            var fraction = (plane.Altitude - light.EmitterAltitude) / span;
            var receiving = span > 0f && plane.Map == light.Target.Map;
            if (!receiving && fraction is <= 0f or >= 1f)
                continue;

            var radius = light.Reach * MathF.Min(1f, fraction * 1.1f) + SourceRadius + 0.5f;
            if (DiskIsSolid(plane.Map, light.Position, radius))
                return true;
        }

        return false;
    }

    private bool DiskIsSolid(EntityUid map, Vector2 center, float radius)
    {
        _grids.Clear();
        _map.FindGridsIntersecting(map, Box2.CenteredAround(center, new Vector2(radius * 2f)), ref _grids, approx: true);
        foreach (var grid in _grids)
        {
            var tileSize = grid.Comp.TileSize;
            var local = Vector2.Transform(center, _transform.GetInvWorldMatrix(grid.Owner)) / tileSize;
            var r = radius / tileSize;
            var min = (local - new Vector2(r)).Floored();
            var max = (local + new Vector2(r)).Floored();
            if (CellsSolid(grid, local, r, min, max))
                return true;
        }

        return false;
    }

    private bool CellsSolid(Entity<MapGridComponent> grid, Vector2 local, float r, Vector2i min, Vector2i max)
    {
        for (var y = min.Y; y <= max.Y; y++)
        for (var x = min.X; x <= max.X; x++)
        {
            var nearest = Vector2.Clamp(local, new Vector2(x, y), new Vector2(x + 1, y + 1));
            if ((nearest - local).LengthSquared() > r * r)
                continue;

            if (!_map.TryGetTileRef(grid.Owner, grid.Comp, new Vector2i(x, y), out var tile) ||
                tile.Tile.IsEmpty ||
                _tiles[tile.Tile.TypeId] is not ContentTileDefinition { Transparent: false })
            {
                return false;
            }
        }

        return true;
    }

    // lower bound on altitude-sorted planes
    private static int FirstPlaneAbove(IReadOnlyList<LightPlane> planes, float altitude)
    {
        int lo = 0, hi = planes.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (planes[mid].Altitude <= altitude)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    private bool DrawCloudMask(DrawingHandleWorld handle, ProjectedLight light, Vector2 emitter, float cloudTop, float span, int pass)
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
        shader.SetParameter("cloudCenter", Vector2.Lerp(emitter, light.Position, midFraction));
        shader.SetParameter("cloudReach", light.Reach * midFraction);
        shader.SetParameter("cloudOverlap", (upper - lower) / CloudThickness);

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(shader);
        handle.DrawRect(new Box2(0, 0, MaskSize, MaskSize), Color.White);
        handle.UseShader(null);
        return true;
    }

    private void DrawFloorBlockers(DrawingHandleWorld handle, ProjectedLight light, Vector2 emitter, PlaneGeometry geometry,
        float fraction)
    {
        var scale = MaskSize / (2f * light.Reach);
        var shift = (emitter - light.Position) * scale;
        var matrix = Matrix3x2.CreateTranslation(-emitter) *
                     Matrix3x2.CreateScale(scale / fraction, -scale / fraction) *
                     Matrix3x2.CreateTranslation(MaskSize / 2f + shift.X, MaskSize / 2f - shift.Y);

        for (var i = 0; i < geometry.GridCount; i++)
        {
            var grid = geometry.Grids[i];
            handle.SetTransform(grid.World * matrix);
            foreach (var tile in grid.Tiles)
                handle.DrawRect(tile, Color.Black);
        }
    }

    private void DrawWallBlockers(DrawingHandleWorld handle, ProjectedLight light, Vector2 emitter, PlaneGeometry geometry,
        float start, float end)
    {
        var near = MathF.Max(start, 0.0001f);
        var far = MathF.Min(end, 1f);
        if (near >= far)
            return;

        var scale = new Vector2(1f, -1f) * (MaskSize / (2f * light.Reach));
        var center = new Vector2(MaskSize / 2f) + (emitter - light.Position) * scale;
        var corners = _wallCorners;
        handle.SetTransform(Matrix3x2.Identity);

        var vertex = 0;
        foreach (var count in geometry.WallCounts)
        {
            for (var i = 0; i < count; i++)
            {
                var delta = (geometry.WallVertices[vertex + i] - emitter) * scale;
                corners[i] = center + delta / near;
                corners[i + count] = center + delta / far;
            }

            vertex += count;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, WallFaces[count], corners.AsSpan(0, count * 2), Color.Black);
        }
    }

    private PlaneGeometry GetGeometry(ProjectedLight light, EntityUid map)
    {
        if (_timing.CurFrame != _geometryFrame)
        {
            foreach (var stale in _geometry.Values)
            {
                stale.Reset();
                _geometryPool.Push(stale);
            }

            _geometry.Clear();
            _geometryFrame = _timing.CurFrame;
        }

        if (_geometry.TryGetValue((light.Uid, map), out var geometry))
            return geometry;

        geometry = _geometryPool.TryPop(out var pooled) ? pooled : new PlaneGeometry();
        _geometry[(light.Uid, map)] = geometry;
        var bounds = Box2.CenteredAround(light.Position, new Vector2(2f * (light.Source.Radius + SourceRadius)));

        _grids.Clear();
        _map.FindGridsIntersecting(map, bounds, ref _grids, approx: true);
        foreach (var grid in _grids)
        {
            var floor = geometry.NextGrid(_transform.GetWorldMatrix(grid.Owner));
            var tiles = _map.GetTilesEnumerator(grid.Owner, grid.Comp, bounds);
            while (tiles.MoveNext(out var tile))
            {
                if (_tiles[tile.Tile.TypeId] is ContentTileDefinition { Transparent: false })
                    floor.Tiles.Add(_lookup.GetLocalBounds(tile, grid.Comp.TileSize));
            }
        }

        if (!_mapQuery.TryComp(map, out var mapComp))
            return geometry;

        _walls.Clear();
        _occluders.QueryAabb(_walls, mapComp.MapId, bounds);
        foreach (var wall in _walls)
        {
            var polygon = wall.Comp1.Polygon;
            if (!wall.Comp1.Enabled || polygon.Length < 3 || polygon.Length > PhysicsConstants.MaxPolygonVertices)
                continue;

            var matrix = _transform.GetWorldMatrix(wall.Comp2);
            foreach (var point in polygon)
                geometry.WallVertices.Add(Vector2.Transform(point, matrix));
            geometry.WallCounts.Add(polygon.Length);
        }

        return geometry;
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

    private static Vector3[] BuildSampleOffsets()
    {
        var offsets = new Vector3[Samples];
        for (var i = 0; i < Samples; i++)
        {
            var z = 1f - 2f * (i + 0.5f) / Samples;
            var ring = MathF.Sqrt(1f - z * z);
            var angle = i * 2.3999632f;
            var radius = SourceRadius * MathF.Cbrt((i + 0.5f) / Samples);
            offsets[i] = new Vector3(MathF.Cos(angle) * ring, MathF.Sin(angle) * ring, z) * radius;
        }

        return offsets;
    }

    private void CollectEmitters(ProjectedLight light)
    {
        _emitters.Clear();
        var walls = GetGeometry(light, light.SourceMap);
        foreach (var offset in SampleOffsets)
        {
            var emitter = light.Position + new Vector2(offset.X, offset.Y);
            if (!InsideWall(walls, emitter))
                _emitters.Add((emitter, light.EmitterAltitude + offset.Z / DeckSpacing));
        }

        if (_emitters.Count == 0)
            _emitters.Add((light.Position, light.EmitterAltitude));
    }

    private static bool InsideWall(PlaneGeometry geometry, Vector2 point)
    {
        var vertex = 0;
        foreach (var count in geometry.WallCounts)
        {
            var inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var a = geometry.WallVertices[vertex + i];
                var b = geometry.WallVertices[vertex + j];
                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }

            if (inside)
                return true;

            vertex += count;
        }

        return false;
    }

    private void DrawLight(DrawingHandleWorld handle, Matrix3x2 matrix, Texture mask, ShaderInstance shader, ProjectedLight light, Color weight)
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
        handle.DrawTextureRect(mask, new Box2(light.Position - offset, light.Position + offset), weight);
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

    private sealed class PlaneGeometry
    {
        public readonly List<FloorGrid> Grids = new();
        public int GridCount;
        public readonly List<Vector2> WallVertices = new();
        public readonly List<int> WallCounts = new();

        public FloorGrid NextGrid(Matrix3x2 world)
        {
            if (GridCount == Grids.Count)
                Grids.Add(new FloorGrid());

            var grid = Grids[GridCount++];
            grid.World = world;
            grid.Tiles.Clear();
            return grid;
        }

        public void Reset()
        {
            GridCount = 0;
            WallVertices.Clear();
            WallCounts.Clear();
        }
    }

    private sealed class FloorGrid
    {
        public Matrix3x2 World;
        public readonly List<Box2> Tiles = new();
    }
}

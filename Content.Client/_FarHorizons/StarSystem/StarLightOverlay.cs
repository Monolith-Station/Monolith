using System.Numerics;
using System.Runtime.InteropServices;
using Content.Client.Light;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FarHorizons.StarSystem;

/// <summary>
/// I was tempted to paste in a few verses from that one song "Fireflies", but I have restraint.
/// </summary>
public sealed class StarLightOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> StarLightShader = "StarLight";

    /// <summary>
    /// No, diagonal wall corners don't bleed light
    /// </summary>
    private const float ShadowBleed = 0.05f;

    private readonly IEntityManager _entMan;
    private readonly IMapManager _mapMan;
    private readonly IPrototypeManager _protoMan;
    private readonly IOverlayManager _overlayMan;
    private readonly ITileDefinitionManager _tileDefMan;
    private readonly IConfigurationManager _cfg;

    private readonly SharedMapSystem _mapSystem;
    private readonly SharedTransformSystem _xformSystem;

    private readonly OccluderSystem _occluders;
    private readonly EntityQuery<MapGridComponent> _gridQuery;
    private readonly List<Entity<OccluderComponent, TransformComponent>> _occluderResults = new();

    /// <summary>
    /// Walls we've seen before. This is so once the walls get unloaded by PVS, they still occlude so light doesnt bleed through.
    /// It would likely be better to just inform the client of the occluding area somehow, but this is simpler for the time being.
    /// </summary>
    private readonly Dictionary<NetEntity, HashSet<Vector2i>> _remembered = new();
    private readonly Dictionary<NetEntity, (GameTick Tick, List<Vector2i> Tiles)> _transparent = new();

    private HashSet<Vector2i> _known = new();
    private float _trustRange;
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly Vector2[] _extruded = new Vector2[6];

    private readonly List<Vector2> _shadowVerts = new();

    private const int MaxShadowVerts = 12 * 4096;

    private readonly List<(Entity<MapGridComponent> Grid, Matrix3x2 Matrix, Vector2 StarLocal, Vector2 EyeLocal, Color Glow)> _passes = new();
    private ShaderInstance? _shader;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;
    public const int ContentZIndex = BeforeLightTargetOverlay.ContentZIndex + 1;

    public StarLightOverlay(
        IEntityManager entMan,
        IMapManager mapMan,
        IPrototypeManager protoMan,
        IOverlayManager overlayMan,
        ITileDefinitionManager tileDefMan,
        IConfigurationManager cfg)
    {
        _entMan = entMan;
        _mapMan = mapMan;
        _protoMan = protoMan;
        _overlayMan = overlayMan;
        _tileDefMan = tileDefMan;
        _cfg = cfg;

        _mapSystem = entMan.System<SharedMapSystem>();
        _xformSystem = entMan.System<SharedTransformSystem>();
        _occluders = entMan.System<OccluderSystem>();
        _gridQuery = entMan.GetEntityQuery<MapGridComponent>();

        ZIndex = ContentZIndex;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye != null &&
               _entMan.TryGetComponent<StarLightComponent>(args.MapUid, out var light) &&
               light.Enabled &&
               _entMan.TryGetComponent<StarSystemMapComponent>(args.MapUid, out var map) &&
               map.StarSystem != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye!;
        var comp = _entMan.GetComponent<StarLightComponent>(args.MapUid);
        var star = _entMan.GetComponent<StarSystemMapComponent>(args.MapUid).StarSystem!.Star;

        var lightOverlay = _overlayMan.GetOverlay<BeforeLightTargetOverlay>();
        var target = lightOverlay.GetCachedForViewport(args.Viewport).EnlargedLightTarget;
        var bounds = lightOverlay.EnlargedBounds;
        var box = bounds.CalcBoundingBox();

        var viewport = args.Viewport;
        var lightScale = viewport.LightRenderTarget.Size / (Vector2)viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);
        var invMatrix = target.GetWorldToLocalMatrix(eye, scale);

        var mapId = args.MapId;
        var eyePos = eye.Position.Position;

        var power = MathF.Pow(MathF.Max(0f, star.Luminocity), 0.4f);
        power = power / (1f + power) * comp.Intensity;

        _shader ??= _protoMan.Index(StarLightShader).InstanceUnique();
        _shader.SetParameter("starPos", star.Position);
        _shader.SetParameter("starColor", new Vector3(star.Color.R, star.Color.G, star.Color.B));
        _shader.SetParameter("starRange", star.Radius * comp.RangeFactor);
        _shader.SetParameter("starPower", power);
        _shader.SetParameter("starFalloff", comp.Falloff);
        _shader.SetParameter("starCurveFactor", comp.CurveFactor);
        _shader.SetParameter("ambientFloor", new Vector3(comp.AmbientFloor.R, comp.AmbientFloor.G, comp.AmbientFloor.B));
        _shader.SetParameter("boundsMin", box.BottomLeft);
        _shader.SetParameter("boundsSize", box.Size);

        var worldHandle = args.WorldHandle;

        worldHandle.RenderInRenderTarget(target,
            () =>
            {
                worldHandle.SetTransform(invMatrix);
                worldHandle.UseShader(_shader);
                worldHandle.DrawRect(box, Color.White);
                worldHandle.UseShader(null);

                DrawGrids(worldHandle, invMatrix, mapId, bounds, eyePos, comp, star);

                worldHandle.SetTransform(Matrix3x2.Identity);
            }, null);
    }

    private void DrawGrids(
        DrawingHandleWorld handle,
        Matrix3x2 baseMatrix,
        MapId mapId,
        Box2Rotated bounds,
        Vector2 eyeWorld,
        StarLightComponent comp,
        Star star)
    {
        var shadowLength = comp.ShadowLength;

        _trustRange = _cfg.GetCVar(CVars.NetPvsPriorityRange) / 2f;

        var casterBounds = bounds.Enlarged(shadowLength);
        var starRadius = star.Radius * comp.RadiusFactor;
        var range = star.Radius * comp.RangeFactor;

        var power = MathF.Pow(MathF.Max(0f, star.Luminocity), 0.4f);
        power = power / (1f + power) * comp.Intensity;

        _grids.Clear();
        _mapMan.FindGridsIntersecting(mapId, casterBounds, ref _grids, approx: true);

        _passes.Clear();

        foreach (var grid in _grids)
        {
            var (gridPos, gridRot) = _xformSystem.GetWorldPositionRotation(grid.Owner);
            var matrix = Matrix3x2.Multiply(_xformSystem.GetWorldMatrix(grid.Owner), baseMatrix);

            var toStar = star.Position - gridPos;
            var dist = toStar.Length();

            var starLocal = (-gridRot).RotateVec(toStar);

            var lit = Attenuate(dist, range, comp.Falloff, comp.CurveFactor) * power;

            var overStar = dist <= starRadius + grid.Comp.LocalAABB.Size.Length();

            var glowColor = overStar
                ? new Color(star.Color.R * lit, star.Color.G * lit, star.Color.B * lit)
                : Color.Transparent;

            var eyeLocal = (-gridRot).RotateVec(eyeWorld - gridPos);
            _passes.Add((grid, matrix, starLocal, eyeLocal, glowColor));
        }

        var shadowColor = comp.AmbientFloor.WithAlpha(Math.Clamp(comp.ShadowStrength, 0f, 1f));

        if (shadowColor.A > 0f)
        {
            // vertices are already in target space, so nothing further to transform
            handle.SetTransform(Matrix3x2.Identity);
            _shadowVerts.Clear();

            RefreshOccluders(mapId, casterBounds);

            foreach (var pass in _passes)
            {
                _known = GetRemembered(_entMan.GetNetEntity(pass.Grid.Owner));
                DrawShadows(handle, pass.Grid, casterBounds, pass.StarLocal, shadowLength, pass.Matrix, shadowColor);
            }

            FlushShadows(handle, shadowColor);
        }

        foreach (var pass in _passes)
        {
            if (pass.Glow.A <= 0f)
                continue;

            handle.SetTransform(pass.Matrix);
            DrawGlow(handle, pass.Grid, casterBounds, pass.StarLocal, starRadius, pass.Glow);
        }
    }

    private void DrawShadows(
        DrawingHandleWorld handle,
        Entity<MapGridComponent> grid,
        Box2Rotated bounds,
        Vector2 starLocal,
        float shadowLength,
        Matrix3x2 matrix,
        Color shadowColor)
    {
        var tileSize = grid.Comp.TileSize;
        var localBounds = _xformSystem.GetInvWorldMatrix(grid.Owner).TransformBox(bounds);

        foreach (var idx in _known)
        {
            var local = Box2.FromDimensions(idx * tileSize, new Vector2(tileSize, tileSize));

            if (!localBounds.Intersects(local))
                continue;

            var toStar = starLocal - local.Center;
            var dist = toStar.Length();
            var dir = dist < 0.001f ? Vector2.UnitX : toStar / dist;

            if (IsHidden(idx, -Math.Sign(dir.X), -Math.Sign(dir.Y)))
                continue;

            var castOffset = -dir * shadowLength;
            Sweep(local.Enlarged(ShadowBleed), castOffset, _extruded);
            AppendFan(matrix);

            if (_shadowVerts.Count >= MaxShadowVerts)
                FlushShadows(handle, shadowColor);
        }
    }

    private void AppendFan(Matrix3x2 matrix)
    {
        var origin = Vector2.Transform(_extruded[0], matrix);
        var prev = Vector2.Transform(_extruded[1], matrix);

        for (var i = 2; i < 6; i++)
        {
            var current = Vector2.Transform(_extruded[i], matrix);

            _shadowVerts.Add(origin);
            _shadowVerts.Add(prev);
            _shadowVerts.Add(current);

            prev = current;
        }
    }

    private void FlushShadows(DrawingHandleWorld handle, Color shadowColor)
    {
        if (_shadowVerts.Count == 0)
            return;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, CollectionsMarshal.AsSpan(_shadowVerts), shadowColor);
        _shadowVerts.Clear();
    }

    private void DrawGlow(
        DrawingHandleWorld handle,
        Entity<MapGridComponent> grid,
        Box2Rotated bounds,
        Vector2 starLocal,
        float starRadius,
        Color glowColor)
    {
        var tileSize = grid.Comp.TileSize;
        var localBounds = _xformSystem.GetInvWorldMatrix(grid.Owner).TransformBox(bounds);

        foreach (var idx in GetTransparent(grid))
        {
            var local = Box2.FromDimensions(idx * tileSize, new Vector2(tileSize, tileSize));

            if (!localBounds.Intersects(local))
                continue;

            var over = DiscCoverage(local, starLocal, starRadius);

            if (over <= 0f)
                continue;

            handle.DrawRect(local, glowColor.WithAlpha(glowColor.A * over));
        }
    }

    private static float DiscCoverage(Box2 tile, Vector2 centre, float radius)
    {
        var min = float.MaxValue;
        var max = float.MinValue;

        for (var i = 0; i < 4; i++)
        {
            var dist = (Corner(tile, i) - centre).Length();
            min = MathF.Min(min, dist);
            max = MathF.Max(max, dist);
        }

        if (radius <= min)
            return 0f;

        if (radius >= max)
            return 1f;

        return (radius - min) / MathF.Max(max - min, 0.0001f);
    }

    private List<Vector2i> GetTransparent(Entity<MapGridComponent> grid)
    {
        var net = _entMan.GetNetEntity(grid.Owner);

        if (_transparent.TryGetValue(net, out var cached) && cached.Tick == grid.Comp.LastTileModifiedTick)
            return cached.Tiles;

        var tiles = cached.Tiles ?? new List<Vector2i>();
        tiles.Clear();

        var rator = _mapSystem.GetAllTilesEnumerator(grid.Owner, grid.Comp);

        while (rator.MoveNext(out var tileRef))
        {
            if (IsTransparent(tileRef.Value.Tile))
                tiles.Add(tileRef.Value.GridIndices);
        }

        _transparent[net] = (grid.Comp.LastTileModifiedTick, tiles);
        return tiles;
    }

    private bool IsTransparent(Tile tile)
    {
        return tile.IsEmpty || ((ContentTileDefinition)_tileDefMan[tile.TypeId]).Transparent;
    }

    /// <summary>
    /// CPU copy of the attenuation in star_light.swsl.
    /// </summary>
    private static float Attenuate(float dist, float range, float falloff, float curveFactor)
    {
        var sd = Math.Clamp(MathF.Sqrt((dist * dist) + 1f) / range, 0f, 1f);
        var sd2 = sd * sd;
        var curve = float.Lerp(sd, sd2, Math.Clamp(curveFactor, 0f, 1f));

        return Math.Clamp((1f - sd2) * (1f - sd2) / (1f + (falloff * curve)), 0f, 1f);
    }

    /// <summary>
    /// I fucking hate vector math
    /// </summary>
    private static void Sweep(Box2 rect, Vector2 offset, Vector2[] output)
    {
        var k = offset.X >= 0f
            ? (offset.Y >= 0f ? 0 : 3)
            : (offset.Y >= 0f ? 1 : 2);

        output[0] = Corner(rect, k);
        output[1] = Corner(rect, k + 1);
        output[2] = output[1] + offset;
        output[3] = Corner(rect, k + 2) + offset;
        output[4] = Corner(rect, k + 3) + offset;
        output[5] = Corner(rect, k + 3);
    }

    private static Vector2 Corner(Box2 rect, int index)
    {
        return (index % 4) switch
        {
            0 => rect.BottomLeft,
            1 => rect.BottomRight,
            2 => rect.TopRight,
            _ => rect.TopLeft,
        };
    }

    private bool IsHidden(Vector2i idx, int sx, int sy)
    {
        if (sx != 0 && !_known.Contains(idx + new Vector2i(sx, 0)))
            return false;

        if (sy != 0 && !_known.Contains(idx + new Vector2i(0, sy)))
            return false;

        if (sx != 0 && sy != 0 && !_known.Contains(idx + new Vector2i(sx, sy)))
            return false;

        return true;
    }

    /// <summary>
    /// Borrow the engine's perfectly good occluders cache.
    /// </summary>
    private void RefreshOccluders(MapId mapId, Box2Rotated bounds)
    {
        _occluderResults.Clear();
        _occluders.QueryAabb(_occluderResults, mapId, bounds);

        foreach (var pass in _passes)
        {
            var known = GetRemembered(_entMan.GetNetEntity(pass.Grid.Owner));
            var trustSqr = _trustRange * _trustRange;
            var tileSize = pass.Grid.Comp.TileSize;
            var eye = pass.EyeLocal;

            known.RemoveWhere(idx =>
                (((idx + new Vector2(0.5f, 0.5f)) * tileSize) - eye).LengthSquared() <= trustSqr);
        }

        foreach (var occluder in _occluderResults)
        {
            if (!occluder.Comp1.Enabled || occluder.Comp2.GridUid is not { } gridUid)
                continue;

            if (!_gridQuery.TryGetComponent(gridUid, out var gridComp))
                continue;

            var known = GetRemembered(_entMan.GetNetEntity(gridUid));
            known.Add(_mapSystem.TileIndicesFor(gridUid, gridComp, occluder.Comp2.Coordinates));
        }
    }

    public void ResetMemory()
    {
        _remembered.Clear();
        _transparent.Clear();
    }

    private HashSet<Vector2i> GetRemembered(NetEntity grid)
    {
        if (_remembered.TryGetValue(grid, out var known))
            return known;

        known = new HashSet<Vector2i>();
        _remembered[grid] = known;
        return known;
    }
}

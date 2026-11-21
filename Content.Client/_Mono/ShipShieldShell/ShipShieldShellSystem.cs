using System.Linq;
using System.Numerics;
using Content.Client.Viewport;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Mono.Ships;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using static Robust.Client.Graphics.DrawingHandleBase;

namespace Content.Client._Mono.ShipShieldShell;

public sealed partial class ShipShieldShellSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedShipShieldShellSystem _containment = default!;

    private readonly Dictionary<EntityUid, TimeSpan?> _coldStarts = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ShipShieldProjectorComponent, ShipShieldSurfaceContactEvent>(OnSurfaceContact);
        SubscribeLocalEvent<ShipShieldProjectorComponent, ComponentShutdown>(OnProjectorShutdown);
        _overlays.AddOverlay(new ShipShieldShellOverlay(EntityManager, _prototypes, _player, _timing, _tiles, this) { ZIndex = 1 });
    }

    private void OnProjectorShutdown(Entity<ShipShieldProjectorComponent> shield, ref ComponentShutdown args) =>
        _coldStarts.Remove(shield.Owner);

    private void UpdateColdStart(EntityUid uid, ShipShieldProjectorComponent shield, TimeSpan frame)
    {
        if (shield.State == ShipShieldShellState.Starting && !shield.StartupSpoolAt.HasValue)
            _coldStarts.TryAdd(uid, null);
        else if (shield.State != ShipShieldShellState.Active)
            _coldStarts.Remove(uid);
        else if (_coldStarts.TryGetValue(uid, out var started) && !started.HasValue)
            _coldStarts[uid] = frame;
    }

    internal float GetVisualPhase(EntityUid uid, ShipShieldProjectorComponent shield)
    {
        // A cold start has no predicted animation to continue. Start it when power is first visible,
        // not at the server tick in the past. Collision still uses the shared simulation clock.
        if (shield.State == ShipShieldShellState.Active && _coldStarts.GetValueOrDefault(uid) is { } started)
            return Math.Clamp((float) ((_timing.FrameStartTime - started) / shield.AnimationDuration(shield.State)), 0f, 1f);

        return shield.AnimationPhase(_timing.CurTime);
    }

    public override void Shutdown()
    {
        if (_overlays.TryGetOverlay<ShipShieldShellOverlay>(out var overlay))
        {
            _overlays.RemoveOverlay(overlay);
            overlay.Dispose();
        }
    }
}

internal sealed partial class ShipShieldShellOverlay(
    IEntityManager entities, IPrototypeManager prototypes, IPlayerManager player, IGameTiming timing,
    ITileDefinitionManager tiles, ShipShieldShellSystem feedback) : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "MonoShipShieldShell";
    private static readonly ProtoId<ShaderPrototype> ImpactShader = "MonoShipShieldImpact";
    private static readonly Vector2[] Corners =
    {
        new(-1f, -1f), new(1f, -1f), new(1f, 1f), new(-1f, 1f),
    };

    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly CESharedZLevelsSystem _zLevels = entities.System<CESharedZLevelsSystem>();
    private readonly SharedShipShieldShellSystem _shells = entities.System<SharedShipShieldShellSystem>();
    private readonly SharedMapSystem _maps = entities.System<SharedMapSystem>();
    private readonly List<ShaderInstance> _surfaceShaders = new();
    private readonly DrawVertexUV2DColor[] _vertices = new DrawVertexUV2DColor[4];
    private Dictionary<EntityUid, float> _interiorFades = new();
    private Dictionary<EntityUid, float> _nextInteriorFades = new();
    private EntityUid? _fadeObserver;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld | OverlaySpace.WorldSpaceBelowFOV;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var observer = player.LocalEntity;
        if (_fadeObserver != observer)
        {
            _interiorFades.Clear();
            _fadeObserver = observer;
        }

        if (observer is not { } uid || !entities.TryGetComponent(uid, out TransformComponent? observerTransform))
        {
            _interiorFades.Clear();
            return;
        }

        var observerMap = observerTransform.MapUid;
        var observerStack = _shells.GetStack(observerMap);
        var observerPosition = _transform.GetWorldPosition(uid);
        var observerAltitude = _zLevels.GetAbsoluteAltitude(uid);
        _nextInteriorFades.Clear();
        var query = entities.AllEntityQueryEnumerator<ShipShieldProjectorComponent, TransformComponent>();
        while (query.MoveNext(out var emitter, out var shield, out var xform))
        {
            var radii = shield.Shield.Radii;
            if (shield.State is ShipShieldShellState.Off or ShipShieldShellState.Standby ||
                radii is not { X: > 0f, Y: > 0f, Z: > 0f })
                continue;

            var stack = _shells.GetStack(xform.MapUid);
            if (stack == null ? observerMap != xform.MapUid : observerStack != stack)
                continue;

            var target = 0f;
            if (shield.State == ShipShieldShellState.Active)
            {
                if (!_shells.IsSolid(shield))
                    continue;

                var (center, rotation) = _transform.GetWorldPositionRotation(emitter);
                var local = (-rotation).RotateVec(observerPosition - center);
                var height = stack == null ? 0f : observerAltitude - _zLevels.GetAbsoluteAltitude(emitter) - shield.Shield.CenterHeight;
                target = 1f - SmoothFade(0.90f, 1.04f, (new Vector3(local, height) / radii).Length());
            }

            var current = _interiorFades.GetValueOrDefault(emitter);
            var duration = shield.Visuals.InteriorFadeTime;
            var step = duration > 0f ? args.DeltaSeconds / duration : 1f;
            var fade = current + Math.Clamp(target - current, -step, step);
            if (fade > 0f)
                _nextInteriorFades.Add(emitter, fade);
        }

        (_interiorFades, _nextInteriorFades) = (_nextInteriorFades, _interiorFades);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not { } eye || _zPass is { } pass && pass.Viewport != args.Viewport)
            return;

        var splitAtHull = _zPass != null && entities.HasComponent<CEZTransitMapComponent>(args.MapUid);
        var beforeHull = args.Space == OverlaySpace.WorldSpaceBelowWorld;
        if (!splitAtHull && beforeHull)
            return;

        DrawShells(args.WorldHandle, eye, args.WorldAABB, aboveHull: splitAtHull ? !beforeHull : null);
    }

    internal void DrawTransitSurface(IRenderHandle render, IClydeViewport viewport, IClydeViewport transitViewport,
        float opacity, bool? aboveHull = null)
    {
        if (transitViewport.Eye is not { } eye || opacity <= 0f)
            return;

        // Composite the translucent shell directly instead of filtering it with the hull texture.
        eye.GetViewMatrix(out var view, transitViewport.RenderScale);
        var toPixels = view * Matrix3x2.CreateScale(EyeManager.PixelsPerMeter, -EyeManager.PixelsPerMeter) *
            Matrix3x2.CreateTranslation((Vector2) transitViewport.Size / 2f);
        var handle = render.DrawingHandleScreen;
        handle.RenderInRenderTarget(viewport.RenderTarget, () =>
        {
            handle.SetTransform(toPixels);
            DrawShells(handle, eye, default, opacity, projectedOnly: true, aboveHull: aboveHull);
            handle.SetTransform(Matrix3x2.Identity);
        }, null);
    }

    private void DrawShells(DrawingHandleBase handle, IEye eye, Box2 worldBounds,
        float opacity = 1f, bool projectedOnly = false, bool? aboveHull = null)
    {
        var mapId = eye.Position.MapId;
        if (mapId == MapId.Nullspace)
            return;

        var observer = eye is ScalingViewport.ZEye { PlanetTransitBackdrop: true } ? null : player.LocalEntity;
        var lookingUp = entities.TryGetComponent(observer, out CEZLevelViewerComponent? viewer) && viewer.LookUp;
        var observerAltitude = observer is { } altitudeUid ? _zLevels.GetAbsoluteAltitude(altitudeUid) : 0f;
        var observerTransform = observer is { } uidObserver ? entities.GetComponent<TransformComponent>(uidObserver) : null;
        _maps.TryGetMap(mapId, out var drawMap);
        var drawAltitude = observerTransform?.MapUid == drawMap || drawMap == null
            ? observerAltitude
            : _zLevels.GetAbsoluteAltitude(drawMap.Value);
        var surfaceShaderIndex = 0;
        var impactShaderIndex = 0;
        var query = entities.AllEntityQueryEnumerator<ShipShieldProjectorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shield, out var xform))
        {
            var shellRadii = shield.Shield.Radii;
            if (shellRadii is not { X: > 0f, Y: > 0f, Z: > 0f })
                continue;

            var shattering = feedback.TryGetShatterPhase(uid, shield, out var shatterPhase, out var shatterSeed);
            var phase = shattering ? shatterPhase : feedback.GetVisualPhase(uid, shield);
            if (phase <= 0f || phase >= 2f)
                continue;

            var fullyActive = phase >= 1f && _shells.IsSolid(shield);
            var radii = new Vector2(shellRadii.X, shellRadii.Y);
            var emitter = (uid, shield);
            var centerAltitude = _zLevels.GetAbsoluteAltitude(uid) + shield.Shield.CenterHeight;
            var observerHeight = observerAltitude - centerAltitude;
            var stack = _shells.GetStack(xform.MapUid);
            var (center, rotation) = _transform.GetWorldPositionRotation(uid);
            var interior = observer == null ? 0f : SmoothFade(0f, 1f, _interiorFades.GetValueOrDefault(uid));

            var hasSlice = _shells.TryGetSlice(emitter, drawMap, drawAltitude, out var slice);
            var sliceScale = hasSlice ? slice.X / radii.X : 0f;
            var projected = _zPass is { } pass && pass.Map == drawMap && stack != null && _shells.GetStack(pass.Map) == stack;
            var bottom = -1f;
            var top = 1f;
            var underside = projected && observerTransform != null && observerTransform.MapUid != xform.MapUid &&
                observerAltitude < centerAltitude - shield.Shield.CenterHeight;
            var mountedAbove = underside && entities.HasComponent<CEZTransitMapComponent>(xform.MapUid);
            bool drawMesh;
            if (mountedAbove)
            {
                var zPass = _zPass!.Value;
                var cut = observerHeight / shellRadii.Z;
                if (zPass.Map == zPass.ObserverMap)
                {
                    var hullVisible = lookingUp && zPass.Planes.Any(plane => plane.MapUid == xform.MapUid);
                    top = lookingUp && !hullVisible ? 1f : MathF.Min(top, cut);
                    drawMesh = aboveHull != true && bottom < top;
                }
                else
                {
                    bottom = MathF.Max(bottom, cut);
                    drawMesh = lookingUp && zPass.Map == xform.MapUid && bottom < top;
                }
            }
            else
            {
                drawMesh = projected && TryGetSurfaceRange(xform.MapUid, center, shellRadii, centerAltitude,
                    out bottom, out top);
                if (drawMesh && underside && !lookingUp)
                    top = MathF.Min(top, observerHeight / shellRadii.Z);
                drawMesh &= bottom < top;
            }

            if (drawMesh && stack is { } network)
            {
                ClipAgainstTerrain(network, center, rotation, shellRadii, centerAltitude,
                    centerAltitude - shield.Shield.CenterHeight, ref bottom, ref top);
                drawMesh = bottom < top;
            }

            var drawSlice = !projectedOnly && sliceScale > 0.001f && (stack == null || _zPass == null) && xform.MapID == mapId;
            if (!drawMesh && !drawSlice)
                continue;

            var diameter = radii.Length() * 2f;
            if (!projected && !Box2.CenteredAround(center, new Vector2(diameter)).Intersects(worldBounds))
                continue;

            var surfaceOpacity = mountedAbove || projectedOnly && !entities.HasComponent<CEZTransitMapComponent>(xform.MapUid)
                ? 1f
                : opacity;
            var color = shield.Visuals.Color.WithAlpha(shield.Visuals.Color.A * surfaceOpacity);
            var shellColor = color;
            if (shield.MaxCapacity > 0f)
            {
                var charge = shield.Capacity / shield.MaxCapacity;
                shellColor = color.WithAlpha(color.A * (0.25f + 0.75f * charge));
            }
            var shellHullSplit = mountedAbove && drawMap != xform.MapUid ? null : aboveHull;
            // Pack a stable per-break seed above the animation phase in UV2.y.
            var animation = phase;
            if (phase > 1f)
                animation += 2f * Math.Abs((shattering ? shatterSeed : shield.StartedAt).Ticks % 251);

            if (surfaceShaderIndex == _surfaceShaders.Count)
                _surfaceShaders.Add(prototypes.Index(Shader).InstanceUnique());
            var surfaceShader = _surfaceShaders[surfaceShaderIndex++];
            var sign = lookingUp ? -1f : 1f;
            if (drawMesh)
            {
                surfaceShader.SetParameter("MESH_ANIMATION", animation);
                var count = BuildProjectedShell(eye, xform.MapUid, center, rotation, shellRadii, centerAltitude,
                    bottom, top, interior, lookingUp, shellColor, impact: false, shellHullSplit, underside,
                    surfaceShader, shield.Visuals.SpoolOffset);
                handle.UseShader(surfaceShader);
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _surfaceTriangles.AsSpan(0, count));
            }

            var sectionData = (MathF.Round(sliceScale * 1024f) * 2f + interior) * sign;
            if (drawSlice)
            {
                var origin = shield.Visuals.SpoolOffset / radii;
                surfaceShader.SetParameter("MESH_PROJECTION", new Vector4(0f, 0f, origin.X, origin.Y));
                WriteQuad(center, rotation, radii, shellColor, new Vector2(sectionData, animation));
                handle.UseShader(surfaceShader);
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, Texture.White, _vertices);
            }

            if (!fullyActive && !shattering)
                continue;

            CollectWaves(uid, mapId, drawMesh, shattering ? TimeSpan.Zero : shield.StartedAt);
            if (_waves.Count == 0)
                continue;

            var impactVertices = drawMesh
                ? BuildProjectedShell(eye, xform.MapUid, center, rotation, shellRadii, centerAltitude,
                    bottom, top, interior, lookingUp, color, impact: true, shellHullSplit, underside)
                : 0;
            if (!drawMesh)
                WriteQuad(center, rotation, radii, color, new Vector2(0f, sliceScale));

            DrawWaves(handle, drawMesh, impactVertices, radii, underside, shattering ? animation : 1f, ref impactShaderIndex);
        }

        handle.UseShader(null);
    }

    private void WriteQuad(Vector2 center, Angle rotation, Vector2 radii, Color color, Vector2 data)
    {
        for (var i = 0; i < Corners.Length; i++)
        {
            var corner = Corners[i];
            var position = center + rotation.RotateVec(corner * radii);
            _vertices[i] = new DrawVertexUV2DColor(position, (corner + Vector2.One) * 0.5f, color) { UV2 = data };
        }
    }

    private static float SmoothFade(float start, float end, float value)
    {
        var t = Math.Clamp((value - start) / (end - start), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    protected override void DisposeBehavior()
    {
        foreach (var shader in _surfaceShaders)
            shader.Dispose();
        foreach (var batch in _impactBatches)
            batch.Shader.Dispose();
        base.DisposeBehavior();
    }
}

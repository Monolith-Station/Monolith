using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._CE.ZLevels.Light;

public sealed partial class CEZLightProjectionSystem : EntitySystem
{
    private const float LayerHeight = 16f;

    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private PointLightSystem _lights = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<ProjectionKey, EntityUid> _projections = new();
    private readonly Dictionary<EntityUid, List<LightPlane>> _planes = new();
    private readonly HashSet<ProjectionKey> _active = new();
    private readonly List<EntityUid> _sources = new();
    private readonly List<ProjectionKey> _stale = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        BuildPlanes();
        _active.Clear();
        _sources.Clear();

        var query = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (!HasComp<CEZLightProjectionComponent>(uid))
                _sources.Add(uid);
        }

        foreach (var uid in _sources)
        {
            if (!TryComp<PointLightComponent>(uid, out var light) ||
                !TryComp<TransformComponent>(uid, out var xform) ||
                !light.Enabled ||
                light.ContainerOccluded ||
                xform.MapUid is not { } sourceMap ||
                !TryGetPlane(sourceMap, out var network, out _) ||
                !_planes.TryGetValue(network, out var planes))
            {
                continue;
            }

            var sourceAltitude = _zLevels.GetAbsoluteAltitude(uid);
            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            position += rotation.RotateVec(light.Offset);
            GetVerticalBlockers(position, sourceAltitude, light.Radius / LayerHeight, planes, out var lowerBlock, out var upperBlock);

            foreach (var plane in planes)
            {
                if (plane.Map == sourceMap)
                    continue;

                var verticalDistance = MathF.Abs(plane.Altitude - sourceAltitude) * LayerHeight;
                var radiusSquared = light.Radius * light.Radius - verticalDistance * verticalDistance;
                if (verticalDistance <= 0.001f ||
                    radiusSquared <= 1f ||
                    (plane.Altitude < sourceAltitude && lowerBlock > plane.Altitude + 0.001f) ||
                    (plane.Altitude > sourceAltitude && upperBlock <= plane.Altitude + 0.001f))
                    continue;

                var radius = MathF.Sqrt(radiusSquared);
                var key = new ProjectionKey(uid, plane.Map);
                _active.Add(key);
                UpdateProjection(key, plane.Map, position, rotation, radius, verticalDistance, light);
            }
        }

        _stale.Clear();
        foreach (var (key, proxy) in _projections)
        {
            if (_active.Contains(key))
                continue;

            if (!TerminatingOrDeleted(proxy))
                QueueDel(proxy);
            _stale.Add(key);
        }

        foreach (var key in _stale)
            _projections.Remove(key);
    }

    private void BuildPlanes()
    {
        _planes.Clear();

        var networks = EntityQueryEnumerator<CEZMapNetworkComponent>();
        while (networks.MoveNext(out var networkUid, out var network))
        {
            var planes = new List<LightPlane>(network.SortedZLevels.Count);
            _planes.Add(networkUid, planes);

            foreach (var mapUid in network.SortedZLevels)
            {
                if (TryComp<CEZMapComponent>(mapUid, out var map) && HasComp<MapComponent>(mapUid))
                    planes.Add(new LightPlane(mapUid, map.Depth));
            }
        }

        var transits = EntityQueryEnumerator<CEZTransitMapComponent, MapComponent>();
        while (transits.MoveNext(out var mapUid, out var transit, out _))
        {
            if (TryGetTransitPlane(transit, out var network, out var altitude) &&
                _planes.TryGetValue(network, out var planes))
            {
                planes.Add(new LightPlane(mapUid, altitude));
            }
        }
    }

    private bool TryGetPlane(EntityUid mapUid, out EntityUid network, out float altitude)
    {
        if (TryComp<CEZMapComponent>(mapUid, out var map))
        {
            network = map.NetworkUid;
            altitude = map.Depth;
            return true;
        }

        if (TryComp<CEZTransitMapComponent>(mapUid, out var transit))
            return TryGetTransitPlane(transit, out network, out altitude);

        network = default;
        altitude = default;
        return false;
    }

    private bool TryGetTransitPlane(CEZTransitMapComponent transit, out EntityUid network, out float altitude)
    {
        if (transit.LowerMap is { } lower && TryComp<CEZMapComponent>(lower, out var lowerMap))
        {
            network = lowerMap.NetworkUid;
            altitude = lowerMap.Depth + _zLevels.GetTransitProgress(transit);
            return true;
        }

        if (transit.UpperMap is { } upper && TryComp<CEZMapComponent>(upper, out var upperMap))
        {
            network = upperMap.NetworkUid;
            altitude = upperMap.Depth - 1f + _zLevels.GetTransitProgress(transit);
            return true;
        }

        network = default;
        altitude = default;
        return false;
    }

    private void GetVerticalBlockers(
        Vector2 position,
        float sourceAltitude,
        float radius,
        List<LightPlane> planes,
        out float lowerBlock,
        out float upperBlock)
    {
        lowerBlock = float.NegativeInfinity;
        upperBlock = float.PositiveInfinity;

        foreach (var plane in planes)
        {
            if (MathF.Abs(plane.Altitude - sourceAltitude) >= radius || !IsOpaqueAt(plane.Map, position))
                continue;

            if (plane.Altitude <= sourceAltitude + 0.001f)
                lowerBlock = MathF.Max(lowerBlock, plane.Altitude);
            else
                upperBlock = MathF.Min(upperBlock, plane.Altitude);
        }
    }

    private bool IsOpaqueAt(EntityUid mapUid, Vector2 position)
    {
        if (!_mapManager.TryFindGridAt(mapUid, position, out var gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, position, out var tile))
        {
            return false;
        }

        return !((ContentTileDefinition) _tiles[tile.Tile.TypeId]).Transparent;
    }

    private void UpdateProjection(
        ProjectionKey key,
        EntityUid targetMap,
        Vector2 position,
        Angle rotation,
        float radius,
        float verticalDistance,
        PointLightComponent source)
    {
        if (!_projections.TryGetValue(key, out var proxy) ||
            TerminatingOrDeleted(proxy) ||
            !TryComp<PointLightComponent>(proxy, out var light))
        {
            if (!TryComp<MapComponent>(targetMap, out var map))
                return;

            proxy = Spawn(null, new MapCoordinates(position, map.MapId));
            AddComp<CEZLightProjectionComponent>(proxy);
            light = CopyComp(key.Source, proxy, source);
            _projections[key] = proxy;
        }
        else if (light.MaskPath != source.MaskPath ||
                 light.MaskAutoRotate != source.MaskAutoRotate ||
                 light.Rotation != source.Rotation)
        {
            RemComp<PointLightComponent>(proxy);
            light = CopyComp(key.Source, proxy, source);
        }

        _transform.SetWorldPosition(proxy, position);
        _transform.SetWorldRotationNoLerp(proxy, rotation);

        _lights.SetEnabled(proxy, true, light);
        _lights.SetRadius(proxy, radius, light);
        _lights.SetColor(proxy, source.Color, light);
        _lights.SetEnergy(proxy, ProjectedEnergy(source, radius, verticalDistance), light);
        _lights.SetSoftness(proxy, source.Softness, light);
        _lights.SetFalloff(proxy, source.Falloff, light);
        _lights.SetCurveFactor(proxy, source.CurveFactor, light);
        _lights.SetCastShadows(proxy, source.CastShadows, light);

        light.Offset = Vector2.Zero;
    }

    public override void Shutdown()
    {
        foreach (var proxy in _projections.Values)
        {
            if (!TerminatingOrDeleted(proxy))
                Del(proxy);
        }

        _projections.Clear();
        base.Shutdown();
    }

    private static float ProjectedEnergy(PointLightComponent source, float projectedRadius, float verticalDistance)
    {
        var desired = Attenuation(source.Radius, verticalDistance * verticalDistance, source.Falloff, source.CurveFactor);
        var local = Attenuation(projectedRadius, 0f, source.Falloff, source.CurveFactor);
        return local > 0f ? source.Energy * desired / local : 0f;
    }

    private static float Attenuation(float radius, float verticalDistanceSquared, float falloff, float curve)
    {
        var s = Math.Clamp(MathF.Sqrt(1f + verticalDistanceSquared) / radius, 0f, 1f);
        var s2 = s * s;
        var curveFactor = MathHelper.Lerp(s, s2, Math.Clamp(curve, 0f, 1f));
        return Math.Clamp((1f - s2) * (1f - s2) / (1f + falloff * curveFactor), 0f, 1f);
    }

    private readonly record struct ProjectionKey(EntityUid Source, EntityUid TargetMap);
    private readonly record struct LightPlane(EntityUid Map, float Altitude);
}

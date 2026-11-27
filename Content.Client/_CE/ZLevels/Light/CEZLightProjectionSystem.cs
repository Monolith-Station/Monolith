using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client._CE.ZLevels.Light;

public sealed partial class CEZLightProjectionSystem : EntitySystem
{
    internal const float DeckSpacing = 3f;
    private const float EmitterHeight = 1f;

    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private readonly Dictionary<EntityUid, List<LightPlane>> _planes = new();
    private readonly Dictionary<EntityUid, (EntityUid Network, float Altitude)> _mapPlanes = new();
    private readonly Dictionary<EntityUid, List<ZLight>> _lights = new();
    private CEZProjectedLightOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(TransformSystem));
        UpdatesAfter.Add(typeof(AnimationPlayerSystem));
        _overlays.AddOverlay(_overlay = new CEZProjectedLightOverlay(EntityManager, this));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        BuildPlanes();
        foreach (var list in _lights.Values)
            list.Clear();

        var query = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var light, out var xform))
        {
            if (!light.Enabled ||
                light.ContainerOccluded ||
                light.Energy <= 0f ||
                xform.MapUid is not { } sourceMap ||
                !_mapPlanes.TryGetValue(sourceMap, out var plane))
            {
                continue;
            }

            var sourceAltitude = plane.Altitude;
            // Transit planes already include their grid's elevation.
            if (!HasComp<CEZTransitMapComponent>(sourceMap))
                sourceAltitude += _zLevels.GetLocalAltitude(uid);

            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            position += rotation.RotateVec(light.Offset);

            if (!_lights.TryGetValue(plane.Network, out var list))
                _lights[plane.Network] = list = new List<ZLight>();

            list.Add(new ZLight(uid, sourceMap, position, rotation, sourceAltitude + EmitterHeight / DeckSpacing, light));
        }
    }

    internal void CollectProjected(EntityUid targetMap, List<ProjectedLight> output)
    {
        if (!_mapPlanes.TryGetValue(targetMap, out var target) ||
            !_lights.TryGetValue(target.Network, out var lights))
        {
            return;
        }

        var planes = _planes[target.Network];
        var targetPlane = new LightPlane(targetMap, target.Altitude);
        foreach (var light in lights)
        {
            if (light.SourceMap == targetMap)
                continue;

            var height = (light.EmitterAltitude - target.Altitude) * DeckSpacing;
            var heightSquared = height * height;
            var reachSquared = light.Source.Radius * light.Source.Radius - heightSquared;
            if (reachSquared > 0f)
            {
                output.Add(new ProjectedLight(light.Uid, light.SourceMap, targetPlane, planes, light.Position,
                    light.Rotation, light.EmitterAltitude, heightSquared, MathF.Sqrt(reachSquared), light.Source));
            }
        }
    }

    private void BuildPlanes()
    {
        _mapPlanes.Clear();
        foreach (var list in _planes.Values)
            list.Clear();

        var networks = EntityQueryEnumerator<CEZMapNetworkComponent>();
        while (networks.MoveNext(out var networkUid, out var network))
        {
            foreach (var mapUid in network.SortedZLevels)
            {
                if (TryComp<CEZMapComponent>(mapUid, out var map) && HasComp<MapComponent>(mapUid))
                    AddPlane(networkUid, mapUid, map.Depth);
            }
        }

        var transits = EntityQueryEnumerator<CEZTransitMapComponent, MapComponent>();
        while (transits.MoveNext(out var mapUid, out var transit, out _))
        {
            if (TryGetTransitPlane(transit, out var network, out var altitude))
                AddPlane(network, mapUid, altitude);
        }

        foreach (var list in _planes.Values)
            list.Sort(static (a, b) => a.Altitude.CompareTo(b.Altitude));
    }

    private void AddPlane(EntityUid network, EntityUid map, float altitude)
    {
        if (!_planes.TryGetValue(network, out var list))
            _planes[network] = list = new List<LightPlane>();

        list.Add(new LightPlane(map, altitude));
        _mapPlanes[map] = (network, altitude);
    }

    private bool TryGetTransitPlane(CEZTransitMapComponent transit, out EntityUid network, out float altitude)
    {
        if (transit.LowerMap is { } lower && TryComp<CEZMapComponent>(lower, out var lowerMap))
        {
            network = lowerMap.NetworkUid;
            altitude = lowerMap.Depth + _zLevels.GetTransitProgress(transit) * _zLevels.GetTransitSpan(transit);
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

    public override void Shutdown()
    {
        if (_overlay is { } overlay)
        {
            _overlays.RemoveOverlay(overlay);
            overlay.Dispose();
            _overlay = null;
        }
        base.Shutdown();
    }

    internal readonly record struct LightPlane(EntityUid Map, float Altitude);
    private readonly record struct ZLight(
        EntityUid Uid,
        EntityUid SourceMap,
        Vector2 Position,
        Angle Rotation,
        float EmitterAltitude,
        PointLightComponent Source);
    internal readonly record struct ProjectedLight(
        EntityUid Uid,
        EntityUid SourceMap,
        LightPlane Target,
        IReadOnlyList<LightPlane> Planes,
        Vector2 Position,
        Angle Rotation,
        float EmitterAltitude,
        float HeightSquared,
        float Reach,
        PointLightComponent Source);
}

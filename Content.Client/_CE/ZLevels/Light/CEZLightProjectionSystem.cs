using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client._CE.ZLevels.Light;

public sealed partial class CEZLightProjectionSystem : EntitySystem
{
    private const float DeckSpacing = 3f;
    private const float EmitterHeight = 1f;

    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private readonly Dictionary<EntityUid, List<LightPlane>> _planes = new();
    private readonly List<ProjectedLight> _projectedLights = new();
    private CEZProjectedLightOverlay _overlay = default!;

    internal IReadOnlyList<ProjectedLight> ProjectedLights => _projectedLights;

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
        _projectedLights.Clear();

        var query = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var light, out var xform))
        {
            if (!light.Enabled ||
                light.ContainerOccluded ||
                light.Energy <= 0f ||
                xform.MapUid is not { } sourceMap ||
                !TryGetPlane(sourceMap, out var network, out var sourceAltitude) ||
                !_planes.TryGetValue(network, out var planes))
            {
                continue;
            }

            // Transit planes already include their grid's elevation.
            if (!HasComp<CEZTransitMapComponent>(sourceMap))
                sourceAltitude += _zLevels.GetLocalAltitude(uid);

            var emitterAltitude = sourceAltitude + EmitterHeight / DeckSpacing;
            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            position += rotation.RotateVec(light.Offset);

            foreach (var plane in planes)
            {
                if (plane.Map == sourceMap)
                    continue;

                var height = (emitterAltitude - plane.Altitude) * DeckSpacing;
                var heightSquared = height * height;
                var reachSquared = light.Radius * light.Radius - heightSquared;
                if (reachSquared > 0f)
                {
                    _projectedLights.Add(new ProjectedLight(plane, planes, position, rotation, emitterAltitude,
                        heightSquared, MathF.Sqrt(reachSquared), light));
                }
            }
        }
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

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_overlay);
        _overlay.Dispose();
        base.Shutdown();
    }

    internal readonly record struct LightPlane(EntityUid Map, float Altitude);
    internal readonly record struct ProjectedLight(
        LightPlane Target,
        IReadOnlyList<LightPlane> Planes,
        Vector2 Position,
        Angle Rotation,
        float EmitterAltitude,
        float HeightSquared,
        float Reach,
        PointLightComponent Source);
}

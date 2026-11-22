using System.Numerics;
using Content.Client._CE.Planets.Hellpods;
using Content.Client._CE.Planets.Shields;
using Content.Client.Parallax;
using Content.Client.Viewport;
using Content.Shared._CE.Planets.Hellpods;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._FarHorizons.StarSystem;

public sealed class PlanetOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _protoMan;
    private readonly IPlayerManager _player;
    private readonly PlanetTransitSystem _transits;
    private readonly CEHellpodDepartureSystem _departures;
    private readonly CEPlanetShieldVisualSystem _shields;
    private readonly List<(int Index, Planet Planet, ShaderInstance Shader)> _shaders = new(); // what do you mean, this isn't No Man's Sky?
    private List<Planet>? _planets;
    private readonly Dictionary<int, EntityUid> _planetEntities = new();
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public PlanetOverlay(IEntityManager entMan, IPrototypeManager protoMan)
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        _entMan = entMan;
        _protoMan = protoMan;
        _player = IoCManager.Resolve<IPlayerManager>();
        _transits = entMan.System<PlanetTransitSystem>();
        _departures = entMan.System<CEHellpodDepartureSystem>();
        _shields = entMan.System<CEPlanetShieldVisualSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var systemMap = args.MapUid;
        if (_entMan.TryGetComponent<CEHellpodApproachComponent>(args.MapUid, out var approach))
        {
            if (!_entMan.TryGetComponent<PlanetBodyComponent>(approach.Planet, out var body))
                return false;

            systemMap = body.StarSystemMap;
        }
        else if (args.Viewport.Eye is ScalingViewport.ZEye { DrawParallax: false } ||
                 _entMan.HasComponent<PlanetTransitMapComponent>(args.MapUid))
            return false;

        if (!_entMan.TryGetComponent<StarSystemMapComponent>(systemMap, out var starSystem) ||
            starSystem.StarSystem is not { } system ||
            system.Planets.Count == 0)
        {
            ResetShader();
            return false;
        }

        if (_planets != system.Planets)
        {
            ResetShader();
            _planets = system.Planets;

            for (var i = 0; i < _planets.Count; i++)
            {
                var planet = _planets[i];
                if (SetupPlanetShader(planet, system.Star) is { } shader)
                    _shaders.Add((i, planet, shader));
            }
        }

        _planetEntities.Clear();

        var bodies = _entMan.AllEntityQueryEnumerator<PlanetBodyComponent, TransformComponent>();
        while (bodies.MoveNext(out var uid, out var body, out var xform))
        {
            if (body.StarSystemMap == systemMap && (uint) body.Index < (uint) system.Planets.Count)
            {
                system.Planets[body.Index].Position = xform.LocalPosition;
                _planetEntities[body.Index] = uid;
            }
        }

        var origin = args.WorldAABB.Center;

        _shaders.Sort((a, b) =>
            (origin - b.Planet.Position).LengthSquared().CompareTo((origin - a.Planet.Position).LengthSquared()));

        return _shaders.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _entMan.TryGetComponent<CEHellpodApproachComponent>(args.MapUid, out var approach);
        var handle = args.WorldHandle;
        var viewportBounds = args.WorldAABB;
        var parallaxCenter = args.Viewport.Eye?.Position.Position ?? viewportBounds.Center;

        CEHellpodComponent? riderPod = null;
        if (approach != null &&
            _entMan.TryGetComponent<TransformComponent>(_player.LocalEntity, out var rider) &&
            rider.MapUid == args.MapUid &&
            _entMan.TryGetComponent<CEHellpodComponent>(rider.ParentUid, out var pod) && pod.InFlight)
            riderPod = pod;

        var (transitIndex, transitProgress) = _transits.GetPlanetApproach(args.MapUid);
        foreach (var (index, planet, shader) in _shaders)
        {
            var hasBody = _planetEntities.TryGetValue(index, out var uid);
            if (approach != null && (!hasBody || uid != approach.Planet))
                continue;

            var (parallax, radiusScale) = planet.GetProjection();
            var baseRadius = planet.Radius * radiusScale;
            var progress = index == transitIndex ? transitProgress : 0f;
            var projection = approach == null
                ? PlanetViewProjection.Orbit(parallaxCenter, planet.Position, parallax, baseRadius, progress)
                : _departures.GetRiderProjection(approach, riderPod, parallaxCenter,
                    planet.Position, parallax, baseRadius).Projection;

            shader.SetParameter("planetPos", planet.Position);
            shader.SetParameter("viewportMin", viewportBounds.BottomLeft);
            shader.SetParameter("viewportSize", viewportBounds.Size);
            shader.SetParameter("projectedCenter", projection.Center);
            shader.SetParameter("projectedRadius", projection.Radius);
            if (CEPlanetShieldVisualSystem.SupportsShader(planet.Shader))
                _shields.Apply(shader, hasBody ? uid : null, args.MapUid, approach, riderPod);

            handle.UseShader(shader);
            handle.DrawRect(viewportBounds, Color.White);
        }
        handle.UseShader(null);
    }

    private ShaderInstance? SetupPlanetShader(Planet planet, Star star)
    {
        if (!_protoMan.TryIndex<ShaderPrototype>(planet.Shader, out var shaderProto) ||
            !_protoMan.TryIndex(planet.Palette, out var palette))
            return null;

        var shader = shaderProto.InstanceUnique();
        if (CEPlanetShieldVisualSystem.SupportsShader(planet.Shader))
            CEPlanetShieldVisualSystem.InitializeShader(shader);

        // Planet physical info
        shader.SetParameter("planetPos", planet.Position);
        shader.SetParameter("rotationAngle", planet.Rotation);

        // Colors
        // What colors do is up to shader
        var color1 = new Vector3(palette.Color1.R, palette.Color1.G, palette.Color1.B);
        shader.SetParameter("color1", color1);
        var color2 = new Vector3(palette.Color2.R, palette.Color2.G, palette.Color2.B);
        shader.SetParameter("color2", color2);
        var color3 = new Vector3(palette.Color3.R, palette.Color3.G, palette.Color3.B);
        shader.SetParameter("color3", color3);
        var color4 = new Vector3(palette.Color4.R, palette.Color4.G, palette.Color4.B);
        shader.SetParameter("color4", color4);

        shader.SetParameter("hueShift", planet.HueShift);
        shader.SetParameter("saturationShift", planet.SaturationShift);

        // Star info
        var starPos = star.Position;
        var starColor = new Vector3(star!.Color.R, star!.Color.G, star!.Color.B);
        shader.SetParameter("starPos", starPos);
        shader.SetParameter("starColor", starColor);
        shader.SetParameter("starLuminosity", star.Luminocity);

        // Atmosphere
        if (planet.Atmosphere != null)
        {
            shader.SetParameter("hasAtmosphere", true);

            var atmosColor = new Vector3(planet.Atmosphere.Color.R, planet.Atmosphere.Color.G, planet.Atmosphere.Color.B);
            shader.SetParameter("atmosphereColor", atmosColor);

            shader.SetParameter("atmosphereThickness", planet.Atmosphere.Thickness);
            shader.SetParameter("atmosphereDensity", planet.Atmosphere.Density);

            var cloudColor = new Vector3(planet.Atmosphere.CloudColor.R, planet.Atmosphere.CloudColor.G, planet.Atmosphere.CloudColor.B);
            shader.SetParameter("cloudColor", cloudColor);

            shader.SetParameter("cloudCoverage", planet.Atmosphere.CloudCoverage);
            shader.SetParameter("cloudScale", planet.Atmosphere.CloudScale);
            shader.SetParameter("cloudDensity", planet.Atmosphere.CloudDensity);
        }
        else
        {
            shader.SetParameter("hasAtmosphere", false);
        }

        // Liquid
        if (planet.Liquid != null)
        {
            shader.SetParameter("liquidLevel", planet.Liquid.Level);
            shader.SetParameter("riverFrequency", planet.Liquid.RiverFrequency);
            shader.SetParameter("riverThreshold", planet.Liquid.RiverThreshold);
            shader.SetParameter("liquidSpecularity", planet.Liquid.Specularity);
            shader.SetParameter("isLiquidEmissive", planet.Liquid.Emmissive);
            shader.SetParameter("liquidEmission", planet.Liquid.Emission);

            var color = new Vector3(planet.Liquid.Color.R, planet.Liquid.Color.G, planet.Liquid.Color.B);
            shader.SetParameter("liquidColor", color);

            var shallowColor = new Vector3(planet.Liquid.ShallowColor.R, planet.Liquid.ShallowColor.G, planet.Liquid.ShallowColor.B);
            shader.SetParameter("liquidShallowColor", shallowColor);
        }
        else
        {
            shader.SetParameter("liquidLevel", -0.02f); // This is so jank...
            shader.SetParameter("riverThreshold", 0f);
        }

        // Ring info
        if (planet.Rings != null)
        {
            shader.SetParameter("hasRings", true);
            shader.SetParameter("ringsRadiusInner", planet.Rings.RadiusInner);
            shader.SetParameter("ringsRadiusOuter", planet.Rings.RadiusOuter);
            shader.SetParameter("ringsBandFrequency", planet.Rings.BandFrequency);

            var ringsColor1 = new Vector3(planet.Rings.Color1.R, planet.Rings.Color1.G, planet.Rings.Color1.B);
            shader.SetParameter("ringsColor1", ringsColor1);
            var ringsColor2 = new Vector3(planet.Rings.Color2.R, planet.Rings.Color2.G, planet.Rings.Color2.B);
            shader.SetParameter("ringsColor2", ringsColor2);
            var ringsColor3 = new Vector3(planet.Rings.Color3.R, planet.Rings.Color3.G, planet.Rings.Color3.B);
            shader.SetParameter("ringsColor3", ringsColor3);
        } else
        {
            shader.SetParameter("hasRings", false);
        }

        // Custom data
        // each planet type will have different data
        // it's up to whoever adds more to make sure corrent inputs exist
        foreach (var (key, value) in planet.CustomData.Floats)
        {
            if (key is not ("parallaxFactor" or "planetaryRadiusFactor"))
                shader.SetParameter(key, value);
        }

        foreach (var (key, value) in planet.CustomData.Ints)
            shader.SetParameter(key, value);

        foreach (var (key, value) in planet.CustomData.Colors)
        {
            var color = new Vector3(value.R, value.G, value.B);
            shader.SetParameter(key, color);
        }

        return shader;
    }

    public void ResetShader()
    {
        _planets = null;
        _planetEntities.Clear();
        foreach (var (_, _, shader) in _shaders)
            shader.Dispose();
        _shaders.Clear();
    }
}

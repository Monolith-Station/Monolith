using System.Numerics;
using Content.Client.Parallax;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._FarHorizons.StarSystem;

public sealed class PlanetOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _protoMan;
    private readonly List<(Planet Planet, ShaderInstance Shader)> _shaders = new(); // what do you mean, this isn't No Man's Sky?
    private List<Planet>? _planets;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public PlanetOverlay(IEntityManager entMan, IPrototypeManager protoMan)
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        _entMan = entMan;
        _protoMan = protoMan;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entMan.TryGetComponent<StarSystemMapComponent>(args.MapUid, out var starSystem) ||
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

            foreach (var planet in _planets)
            {
                if (SetupPlanetShader(planet, system.Star) is { } shader)
                    _shaders.Add((planet, shader));
            }
        }

        var origin = args.WorldAABB.Center;

        _shaders.Sort((a, b) =>
            (origin - b.Planet.Position).LengthSquared().CompareTo((origin - a.Planet.Position).LengthSquared()));

        return _shaders.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var viewportBounds = args.WorldAABB;
        var parallaxCenter = args.Viewport.Eye?.Position.Position ?? viewportBounds.Center;
        foreach (var (_, shader) in _shaders)
        {
            shader.SetParameter("viewportMin", viewportBounds.BottomLeft);
            shader.SetParameter("viewportSize", viewportBounds.Size);
            shader.SetParameter("parallaxCenter", parallaxCenter);

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

        // Planet physical info
        shader.SetParameter("planetPos", planet.Position);
        shader.SetParameter("planetRadius", planet.Radius);
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
            shader.SetParameter(key, value);

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
        _shaders.Clear();
    }
}

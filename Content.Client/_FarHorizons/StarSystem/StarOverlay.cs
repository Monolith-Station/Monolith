using System.Numerics;
using Content.Client.Parallax;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._FarHorizons.StarSystem;

public sealed class StarOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _protoMan;

    private Star? _star = null;
    private ShaderInstance? _shaderInstance = null;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public StarOverlay(IEntityManager entMan, IPrototypeManager protoMan)
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        _entMan = entMan;
        _protoMan = protoMan;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entMan.TryGetComponent<StarSystemMapComponent>(args.MapUid, out var starSystem) ||
            starSystem.StarSystem == null)
        {
            _star = null;
            _shaderInstance = null;
            return false;
        }

        var star = starSystem.StarSystem.Star;

        if (_star == star)
            return true;

        _shaderInstance = SetupStarShader(star);
        _star = star;
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_shaderInstance == null) return;

        var handle = args.WorldHandle;
        var viewportBounds = args.WorldAABB;

        _shaderInstance.SetParameter("viewportMin", viewportBounds.BottomLeft);
        _shaderInstance.SetParameter("viewportSize", viewportBounds.Size);
        _shaderInstance.SetParameter("parallaxCenter", args.Viewport.Eye?.Position.Position ?? viewportBounds.Center);
        
        handle.UseShader(_shaderInstance);
        handle.DrawRect(viewportBounds, Color.White);
        handle.UseShader(null);
    }

    private ShaderInstance? SetupStarShader(Star star)
    {
        if (!_protoMan.TryIndex<ShaderPrototype>(star.Shader, out var shaderProto))
            return null;
        
        var shader = shaderProto.InstanceUnique();

        var starPos = star.Position;
        var starColor = new Vector3(star!.Color.R, star!.Color.G, star!.Color.B);

        shader.SetParameter("starWorldPos", starPos);
        shader.SetParameter("starRadius", star.Radius);
        shader.SetParameter("starColor", starColor);
        shader.SetParameter("starLuminosity", star.Luminocity);
        shader.SetParameter("rotationAngle", star.Rotation);

        if (star.Rings != null)
        {
            shader.SetParameter("hasRings", true);
            shader.SetParameter("ringsRadiusInner", star.Rings.RadiusInner);
            shader.SetParameter("ringsRadiusOuter", star.Rings.RadiusOuter);
            shader.SetParameter("ringsBandFrequency", star.Rings.BandFrequency);

            var ringsColor1 = new Vector3(star.Rings.Color1.R, star.Rings.Color1.G, star.Rings.Color1.B);
            shader.SetParameter("ringsColor1", ringsColor1);
            var ringsColor2 = new Vector3(star.Rings.Color2.R, star.Rings.Color2.G, star.Rings.Color2.B);
            shader.SetParameter("ringsColor2", ringsColor2);
            var ringsColor3 = new Vector3(star.Rings.Color3.R, star.Rings.Color3.G, star.Rings.Color3.B);
            shader.SetParameter("ringsColor3", ringsColor3);
        }
        else
        {
            shader.SetParameter("hasRings", false);
        }

        return shader;
    }

    public void ResetShader()
    {
        _star = null;
        _shaderInstance = null;
    }
}
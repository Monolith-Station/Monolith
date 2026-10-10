using System.Numerics;
using Content.Client.Parallax;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._FarHorizons.StarSystem;

public sealed class AsteroidBeltOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _protoMan;

    private Star? _star = null;
    private AsteroidBelt? _belt = null;
    private ShaderInstance? _shaderInstance = null;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public AsteroidBeltOverlay(IEntityManager entMan, IPrototypeManager protoMan)
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        _entMan = entMan;
        _protoMan = protoMan;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entMan.TryGetComponent<StarSystemMapComponent>(args.MapUid, out var starSystem) ||
            starSystem.StarSystem == null ||
            starSystem.StarSystem.AsteroidBelt == null)
        {
            _star = null;
            _belt = null;
            _shaderInstance = null;
            return false;
        }

        var belt = starSystem.StarSystem.AsteroidBelt;

        if (_belt == belt)
            return true;

        _star = starSystem.StarSystem.Star;
        _shaderInstance = SetupBeltShader(_star, belt);
        _belt = belt;
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_shaderInstance == null)
            return;
        
        var handle = args.WorldHandle;
        var viewportBounds = args.WorldAABB;
        _shaderInstance.SetParameter("viewportMin", viewportBounds.BottomLeft);
        _shaderInstance.SetParameter("viewportSize", viewportBounds.Size);

        handle.UseShader(_shaderInstance);
        handle.DrawRect(viewportBounds, Color.White);
        handle.UseShader(null);
    }

    private ShaderInstance? SetupBeltShader(Star star, AsteroidBelt belt)
    {
        if (!_protoMan.TryIndex<ShaderPrototype>(belt.Shader, out var shaderProto) ||
            !_protoMan.TryIndex(belt.Palette, out var palette))
            return null;
        
        var shader = shaderProto.InstanceUnique();

        var starPos = star.Position;
        var starColor = new Vector3(star!.Color.R, star!.Color.G, star!.Color.B);

        shader.SetParameter("starWorldPos", starPos);
        shader.SetParameter("starColor", starColor);
        shader.SetParameter("starLuminosity", star.Luminocity);

        var beltPos = belt.Position;

        shader.SetParameter("asteroidBeltPos", beltPos);
        shader.SetParameter("asteroidBeltRadialSize", belt.RadialSize);

        var color1 = new Vector3(palette.Color1.R, palette.Color1.G, palette.Color1.B);
        shader.SetParameter("color1", color1);
        var color2 = new Vector3(palette.Color2.R, palette.Color2.G, palette.Color2.B);
        shader.SetParameter("color2", color2);
        var color3 = new Vector3(palette.Color3.R, palette.Color3.G, palette.Color3.B);
        shader.SetParameter("color3", color3);
        var color4 = new Vector3(palette.Color4.R, palette.Color4.G, palette.Color4.B);
        shader.SetParameter("color4", color4);

        return shader;
    }

    public void ResetShader()
    {
        _belt = null;
        _shaderInstance = null;
    }
}
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

    private Vector2 _starOffset = Vector2.Zero;
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
            _starOffset = Vector2.Zero;
            return false;
        }

        var star = starSystem.StarSystem.Star;

        if (_star == star)
            return true;

        _starOffset = starSystem.StarOffset;
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
        
        handle.UseShader(_shaderInstance);
        handle.DrawRect(viewportBounds, Color.White);
        handle.UseShader(null);
    }

    private ShaderInstance? SetupStarShader(Star star)
    {
        if (!_protoMan.TryIndex<ShaderPrototype>(star.Shader, out var shaderProto))
            return null;
        
        var shader = shaderProto.InstanceUnique();

        var starPos = star.Position + _starOffset;
        var starColor = new Vector3(star!.Color.R, star!.Color.G, star!.Color.B);

        shader.SetParameter("starWorldPos", starPos);
        shader.SetParameter("starRadius", star.Radius);
        shader.SetParameter("starColor", starColor);
        shader.SetParameter("starLuminosity", star.Luminocity);

        return shader;
    }

    public void ResetShader()
    {
        _star = null;
        _shaderInstance = null;
    }
}
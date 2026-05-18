using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Vignette;

public sealed class VignetteOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly ShaderInstance _shader;

    public VignetteOverlay()
    {
        IoCManager.InjectDependencies(this);

        _shader = _prototype.Index<ShaderPrototype>("Vignette").Instance().Duplicate();
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public override bool RequestScreenTexture => true;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("vignette_color", Color.Black.WithAlpha(0.9f));

        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldBounds, Color.White);
        args.WorldHandle.UseShader(null);
    }
}

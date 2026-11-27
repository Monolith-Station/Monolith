using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Vignette;

public sealed partial class VignetteOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly ShaderInstance _shader;

    public VignetteOverlay()
    {
        IoCManager.InjectDependencies(this);

        _shader = _prototype.Index<ShaderPrototype>("Vignette").Instance().Duplicate();
    }

    public override OverlaySpace Space => OverlaySpace.ScreenSpace; // Mono: Kill this class of bug for good

    protected override void Draw(in OverlayDrawArgs args)
    {
        // Mono: Draw into screenspace instead of individual viewports.
        // I hate this overlay so goddamn much.
        _shader.SetParameter("vignette_color", Color.Black.WithAlpha(0.9f));

        args.ScreenHandle.UseShader(_shader);
        args.ScreenHandle.DrawRect(args.ViewportBounds, Color.White);
        args.ScreenHandle.UseShader(null);
    }
}

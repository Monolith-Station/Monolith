using Robust.Client.Graphics;

namespace Content.Client._Mono.BlackFlash;

public sealed partial class BlackFlashEffectSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        _overlay.AddOverlay(new BlackFlashOverlay());
        _overlay.AddOverlay(new BlackFlashImpactOverlay());
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<BlackFlashOverlay>();
        _overlay.RemoveOverlay<BlackFlashImpactOverlay>();
    }
}

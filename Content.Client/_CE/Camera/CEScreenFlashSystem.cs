using Content.Shared._CE.Camera;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._CE.Camera;

public sealed partial class CEScreenFlashSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IGameTiming _timing = default!;

    private CEScreenFlashOverlay? _instance;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CEScreenFlashEvent>(OnScreenFlash);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_instance is not null)
            _overlay.RemoveOverlay(_instance);
        _instance = null;
    }

    private void OnScreenFlash(CEScreenFlashEvent ev)
    {
        Flash(ev.Duration, ev.Color);
    }

    public void Flash(float duration, Color color)
    {
        if (duration <= 0f || color.A <= 0f)
            return;

        if (_instance is null)
        {
            _instance = new CEScreenFlashOverlay(_timing);
            _overlay.AddOverlay(_instance);
        }

        _instance.Merge(_timing.CurTime, duration, color);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_instance is null || !_instance.Finished(_timing.CurTime))
            return;

        _overlay.RemoveOverlay(_instance);
        _instance = null;
    }
}

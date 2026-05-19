using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._Scp.Vignette;

// TODO: Коммон оверлей систем
public sealed class VignetteOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private VignetteOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        AddOverlay();
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    #region Public API

    public void ToggleOverlay()
    {
        if (_overlayManager.HasOverlay<VignetteOverlay>())
            _overlayManager.RemoveOverlay(_overlay);
        else
            _overlayManager.AddOverlay(_overlay);
    }

    public void AddOverlay()
    {
        if (!_overlayManager.HasOverlay<VignetteOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }

    public void RemoveOverlay()
    {
        if (_overlayManager.HasOverlay<VignetteOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }

    #endregion
}

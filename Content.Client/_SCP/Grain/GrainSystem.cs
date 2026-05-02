using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._Scp.Grain;

// TODO: Коммон оверлей систем
public sealed class GrainOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private GrainOverlay _overlay = default!;

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

    #region Pulic API

    public void ToggleOverlay()
    {
        if (_overlayManager.HasOverlay<GrainOverlay>())
            _overlayManager.RemoveOverlay(_overlay);
        else
            _overlayManager.AddOverlay(_overlay);
    }

    public void AddOverlay()
    {
        if (!_overlayManager.HasOverlay<GrainOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }

    public void RemoveOverlay()
    {
        if (_overlayManager.HasOverlay<GrainOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }

    #endregion
}

using Robust.Client.Graphics;
using Robust.Client;
using Robust.Client.Player;
using Content.Shared._CE.Planets.Shields;

namespace Content.Client._CE.Planets.Shields;

public sealed partial class CEShieldBeamSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameController _game = default!;
    [Dependency] private IPlayerManager _players = default!;
    private CEShieldBeamOverlayDispatcher? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new CEShieldBeamOverlayDispatcher(EntityManager);
        _overlays.AddOverlay(_overlay.LowerOverlay);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        if (_overlay != null)
        {
            _overlays.RemoveOverlay(_overlay.LowerOverlay);
            _overlay.LowerOverlay.Dispose();
            _overlays.RemoveOverlay(_overlay);
            _overlay.Dispose();
            _overlay = null;
        }
        base.Shutdown();
    }
}

using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;

namespace Content.Client._Crescent.ShipShields;

public sealed partial class ShipShieldOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlayManager.AddOverlay(new ShipShieldOverlay(EntityManager, _prototypeManager, _resourceCache));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay<ShipShieldOverlay>();
    }
}

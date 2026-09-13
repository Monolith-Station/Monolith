using Robust.Client.Graphics;

namespace Content.Client._Mono.Water;

/// <summary>
/// Registers <see cref="WaterOverlay"/>, which draws the surface for <c>FloorWater</c> tiles.
/// </summary>
public sealed partial class WaterOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlayMan.AddOverlay(new WaterOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay<WaterOverlay>();
    }
}

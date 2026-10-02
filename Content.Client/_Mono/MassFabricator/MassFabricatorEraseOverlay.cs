using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Mono.MassFabricator;

public sealed class MassFabricatorEraseOverlay(MassFabricatorEraseSystem system) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (system.Selection is not { } box)
            return;

        args.ScreenHandle.DrawRect(box, new Color(1f, 0.15f, 0.15f, 0.12f));
        args.ScreenHandle.DrawRect(box, Color.Red, filled: false);
    }
}

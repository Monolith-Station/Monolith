/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Roof;
using Robust.Client.Graphics;

namespace Content.Client._CE.ZLevels.Roof;

/// <inheritdoc/>
public sealed class CEClientRoofSystem : CESharedRoofSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new CEZLevelGridShadowOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<CEZLevelGridShadowOverlay>();
    }
}

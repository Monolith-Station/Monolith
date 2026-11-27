/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Client._CE.ZCollapse.Overlays;
using Content.Shared._CE.ZCollapse.Events;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._CE.ZCollapse;

public sealed partial class CEZCollapseClientSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;

    public Dictionary<NetEntity, Dictionary<Vector2i, int>>? Grids;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CEZCollapseOverlayToggledEvent>(OnOverlayToggled);
        SubscribeNetworkEvent<CEZCollapseOverlaySnapshotEvent>(OnSnapshotUpdate);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<CEZCollapseDebugOverlay>();
    }

    private void OnOverlayToggled(CEZCollapseOverlayToggledEvent ev)
    {
        if (ev.IsEnabled)
            _overlayMan.AddOverlay(new CEZCollapseDebugOverlay());
        else
        {
            _overlayMan.RemoveOverlay<CEZCollapseDebugOverlay>();
            Grids = null;
        }
    }

    private void OnSnapshotUpdate(CEZCollapseOverlaySnapshotEvent ev)
    {
        Grids ??= new Dictionary<NetEntity, Dictionary<Vector2i, int>>();
        foreach (var (grid, tiles) in ev.Grids)
            Grids[grid] = tiles;
    }
}

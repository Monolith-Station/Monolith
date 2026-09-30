/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Client._CE.ZLevels.Core.Overlays;

public sealed partial class CEZLevelDebugOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = null!;
    [Dependency] private IResourceCache _cache = null!;

    private readonly CESharedZLevelsSystem _zLevels;
    private readonly SharedTransformSystem _transform;

    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CEZLevelDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        _zLevels = _entityManager.System<CESharedZLevelsSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();

        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 8);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var uid in _zLevels.ActiveBodies)
        {
            if (!_entityManager.TryGetComponent<CEZPhysicsComponent>(uid, out var zPhys) ||
                !_entityManager.TryGetComponent<TransformComponent>(uid, out var xform))
                continue;

            if (xform.GridUid != xform.ParentUid)
                continue;

            DrawEntityDebug(args, uid, zPhys);
        }

        var gridQuery = _entityManager.EntityQueryEnumerator<CEZGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out _, out var gridNet, out var gridXform))
        {
            if (gridNet.NetworkId == string.Empty)
                continue;

            var gridWorldPos = _transform.GetWorldPosition(gridXform);
            var gridScreenPos = args.ViewportControl?.WorldToScreen(gridWorldPos) ?? Vector2.Zero;
            if (gridScreenPos == Vector2.Zero)
                continue;

            var shortId = gridNet.NetworkId.Length >= 8
                ? gridNet.NetworkId[..8]
                : gridNet.NetworkId;
            args.ScreenHandle.DrawString(_font, gridScreenPos, $"Net: {shortId}", Color.Cyan);
        }
    }

    private void DrawEntityDebug(in OverlayDrawArgs args, EntityUid uid, CEZPhysicsComponent component)
    {
        var worldPos = _transform.GetWorldPosition(uid);
        var screenPos = args.ViewportControl?.WorldToScreen(worldPos) ?? Vector2.Zero;

        if (screenPos == Vector2.Zero)
            return;

        var localPos = float.Round(component.LocalPosition, 2);
        var groundDis = float.Round(component.LocalPosition - component.CachedGroundHeight, 2);
        var velocity = float.Round(component.Velocity, 2);

        var depthText = $"Z: {localPos}\n" +
                        $"G: {groundDis}\n" +
                        $"V: {velocity}\n" +
                        $"S: {component.CachedStickyGround}";

        args.ScreenHandle.DrawString(_font, screenPos, depthText, Color.White);
    }
}

public sealed partial class CEShowZLevelDebugCommand : LocalizedCommands
{
    [Dependency] private IOverlayManager _overlayManager = null!;

    public override string Command => "showzleveldebug";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_overlayManager.HasOverlay<CEZLevelDebugOverlay>())
        {
            _overlayManager.RemoveOverlay<CEZLevelDebugOverlay>();
            return;
        }

        _overlayManager.AddOverlay(new CEZLevelDebugOverlay());
    }
}

/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client._CE.ZCollapse;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._CE.ZCollapse.Overlays;

public sealed partial class CEZCollapseDebugOverlay : Overlay
{
    private const int WhiteCap = 20;
    private const float AliveFloorT = 0.6f;

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IResourceCache _cache = default!;

    private readonly CEZCollapseClientSystem _collapse;
    private readonly SharedTransformSystem _transform;
    private readonly SharedMapSystem _mapSystem;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public CEZCollapseDebugOverlay()
    {
        IoCManager.InjectDependencies(this);
        _collapse = _entityManager.System<CEZCollapseClientSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
        _mapSystem = _entityManager.System<SharedMapSystem>();
        _font = _cache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 8);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        switch (args.Space)
        {
            case OverlaySpace.WorldSpace:
                DrawFill(args);
                break;
            case OverlaySpace.ScreenSpace:
                DrawText(args);
                break;
        }
    }

    private void DrawFill(in OverlayDrawArgs args)
    {
        if (_collapse.Grids == null)
            return;

        var handle = args.WorldHandle;
        foreach (var (netGrid, tiles) in _collapse.Grids)
        {
            var gridUid = _entityManager.GetEntity(netGrid);
            if (!_entityManager.TryGetComponent<TransformComponent>(gridUid, out var gridXform) || gridXform.MapID != args.MapId)
                continue;

            handle.SetTransform(_transform.GetWorldMatrix(gridUid));
            foreach (var (tile, stability) in tiles)
            {
                var t = stability <= 0
                    ? 0f
                    : AliveFloorT + (1f - AliveFloorT) * Math.Clamp((stability - 1f) / (WhiteCap - 1f), 0f, 1f);
                handle.DrawRect(Box2.FromDimensions(new Vector2(tile.X, tile.Y), new Vector2(1, 1)),
                    Color.InterpolateBetween(Color.Red, Color.White, t).WithAlpha(0.35f));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawText(in OverlayDrawArgs args)
    {
        if (_collapse.Grids == null || args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        foreach (var (netGrid, tiles) in _collapse.Grids)
        {
            var gridUid = _entityManager.GetEntity(netGrid);
            if (!_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var grid) ||
                !_entityManager.TryGetComponent<TransformComponent>(gridUid, out var gridXform) ||
                gridXform.MapID != args.MapId)
                continue;

            foreach (var (tile, stability) in tiles)
            {
                var worldPos = _mapSystem.GridTileToWorldPos(gridUid, grid, tile);
                handle.DrawString(_font, args.ViewportControl.WorldToScreen(worldPos), stability.ToString(), Color.Black);
            }
        }
    }
}

/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Client.Graphics;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._CE.ZLevels.Core.Overlays;

/// <summary>
/// Casts drop shadows of the grids overhead onto the z-level being drawn: for the map in a
/// z-render pass, every grid on the levels ABOVE it — real z-levels and the transit maps holding
/// grids mid-descent in the gaps between them — stamps its tile footprint as a dark, tile-shaped
/// overlay. All z-maps share one world coordinate space, so a tile at world (x,y) overhead
/// shadows the same (x,y) here. Shadows fade with each level of distance and stack where
/// structures overlap. Drawn on every pass, so floating grids darken the floor beneath them.
/// Toggle with <c>showzlevelshadows</c>.
/// </summary>
public sealed class CEZLevelShadowOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = null!;
    [Dependency] private readonly IMapManager _mapManager = null!;

    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _transform;
    private readonly CEClientZLevelsSystem _zLevels;

    /// <summary>How many z-levels up to gather shadow casters from.</summary>
    private const int MaxShadowLevels = 3;

    /// <summary>Shadow opacity at one level of distance; it strengthens as distance drops toward 0
    /// (a grid hovering just over the floor) and fades by <see cref="LevelFalloff"/> per level up.</summary>
    private const float BaseAlpha = 0.35f;
    private const float LevelFalloff = 0.55f;

    private List<Entity<MapGridComponent>> _grids = new();

    // Reused each Draw: the chain of z-level maps at and above the pass map. Index i is i levels up,
    // so index 0 is the pass map itself (never drawn — a level doesn't shadow itself).
    private readonly List<EntityUid> _chain = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CEZLevelShadowOverlay()
    {
        IoCManager.InjectDependencies(this);
        _maps = _entityManager.System<SharedMapSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
        _zLevels = _entityManager.System<CEClientZLevelsSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        // Only z-level maps have levels above to cast from; non-z passes fall out here.
        var passMap = args.MapUid;
        if (!_entityManager.TryGetComponent<CEZMapComponent>(passMap, out var passZMap))
            return;

        var passDepth = passZMap.Depth;
        var worldAabb = args.WorldAABB;
        var handle = args.WorldHandle;

        // Walk the z-levels up from the pass map. _chain[i] is i levels above the pass map, and is
        // the set that scopes casters to THIS observer's network (a transit map counts only if its
        // lower anchor is one of these).
        _chain.Clear();
        var cur = passMap;
        for (var i = 0; i <= MaxShadowLevels; i++)
        {
            _chain.Add(cur);
            if (!_entityManager.TryGetComponent<CEZMapComponent>(cur, out var zc) || zc.MapAbove is not { } up)
                break;
            cur = up;
        }

        // Real z-levels above (index 1+): grids there are grounded, so their height above us is just
        // the level difference.
        for (var level = 1; level < _chain.Count; level++)
            DrawMapShadows(handle, worldAabb, _chain[level], AlphaForDistance(level));

        // Transit maps hold grids mid-descent in the gaps. Height above us is the grid's absolute
        // altitude (GetAbsoluteAltitude already folds in the lower-anchor depth + gap progress)
        // minus our depth — so as a ship sinks toward this floor the distance shrinks continuously
        // and the shadow sharpens, then fades toward the level above near the top of the gap.
        var transitQuery = _entityManager.EntityQueryEnumerator<CEZTransitMapComponent>();
        while (transitQuery.MoveNext(out var transitUid, out var transit))
        {
            // Scope to the observer's network + above the pass: the gap's lower anchor must be in
            // the chain we just walked.
            if (transit.LowerMap is not { } lower || !_chain.Contains(lower))
                continue;

            if (transit.PrimaryGrid is not { } primary)
                continue;

            var distance = _zLevels.GetAbsoluteAltitude(primary) - passDepth;
            if (distance <= 0f || distance >= MaxShadowLevels)
                continue;

            DrawMapShadows(handle, worldAabb, transitUid, AlphaForDistance(distance));
        }
    }

    /// <summary>
    /// Shadow opacity at a continuous distance in levels: <see cref="BaseAlpha"/> at one level,
    /// stronger below that (a grid right over the floor), fainter above by <see cref="LevelFalloff"/>
    /// per level. Clamped so a grazing-the-floor caster never blows past a near-opaque shadow.
    /// </summary>
    private static float AlphaForDistance(float distance)
    {
        return Math.Clamp(BaseAlpha * MathF.Pow(LevelFalloff, distance - 1f), 0f, 0.6f);
    }

    private void DrawMapShadows(DrawingHandleWorld handle, Box2 worldAabb, EntityUid mapUid, float alpha)
    {
        if (!_entityManager.TryGetComponent<MapComponent>(mapUid, out var map))
            return;

        var color = Color.Black.WithAlpha(alpha);

        _grids.Clear();
        _mapManager.FindGridsIntersecting(map.MapId, worldAabb, ref _grids);

        foreach (var (gridUid, grid) in _grids)
        {
            var gridRot = _transform.GetWorldRotation(gridUid);
            var half = grid.TileSizeHalfVector;

            // ignoreEmpty (default) skips space tiles, so only real hull/floor tiles cast.
            var tiles = _maps.GetTilesEnumerator(gridUid, grid, worldAabb);
            while (tiles.MoveNext(out var tileRef))
            {
                var center = _maps.GridTileToWorld(gridUid, grid, tileRef.GridIndices).Position;
                var box = new Box2(center - half, center + half);
                handle.DrawRect(new Box2Rotated(box, gridRot, center), color);
            }
        }
    }
}

public sealed class CEShowZLevelShadowsCommand : LocalizedCommands
{
    [Dependency] private readonly IOverlayManager _overlayManager = null!;

    public override string Command => "showzlevelshadows";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_overlayManager.HasOverlay<CEZLevelShadowOverlay>())
        {
            _overlayManager.RemoveOverlay<CEZLevelShadowOverlay>();
            return;
        }

        _overlayManager.AddOverlay(new CEZLevelShadowOverlay());
    }
}

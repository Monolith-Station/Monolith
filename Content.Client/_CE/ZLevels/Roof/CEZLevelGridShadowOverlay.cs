/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client.Light;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._CE.ZLevels.Roof;

public sealed class CEZLevelGridShadowOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _mapSystem;
    private readonly SharedTransformSystem _xformSystem;
    private readonly CESharedZLevelsSystem _zLevel;

    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<(Entity<MapGridComponent> Grid, float Height)> _shadowGrids = new();
    private readonly Dictionary<EntityUid, float> _shadowMapGaps = new();

    public Color Color = Color.Black;

    public const float MaxShadowAlpha = 0.85f;
    public const float ShadowHeightFalloff = 0.5f;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public const int ContentZIndex = RoofOverlay.ContentZIndex + 1;

    public CEZLevelGridShadowOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        IoCManager.InjectDependencies(this);

        _lookup = _entManager.System<EntityLookupSystem>();
        _mapSystem = _entManager.System<SharedMapSystem>();
        _xformSystem = _entManager.System<SharedTransformSystem>();
        _zLevel = _entManager.System<CESharedZLevelsSystem>();

        ZIndex = ContentZIndex;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (eye == null || !_entManager.HasComponent<MapLightComponent>(args.MapUid))
            return;

        if (!_entManager.TryGetComponent(args.MapUid, out CEZLevelMapComponent? zComp))
            return;


        _shadowGrids.Clear();

        CollectShadowGrids(args.MapUid, 0f, args.WorldBounds, requireAirborne: true);

        // Track each visible level's height so transit gaps can anchor to them.
        _shadowMapGaps.Clear();
        _shadowMapGaps[args.MapUid] = 0f;

        var gap = 1f;
        foreach (var aboveMap in _zLevel.GetAllMapsAbove((args.MapUid, zComp)))
        {
            _shadowMapGaps[aboveMap] = gap;
            CollectShadowGrids(aboveMap, gap, args.WorldBounds, requireAirborne: false);
            gap += 1f;
        }

        // Grids in transit shadow this level from whatever gap they're crossing:
        // height = the gap's floor level plus their progress within it.
        var transitQuery = _entManager.EntityQueryEnumerator<CEZTransitMapComponent>();
        while (transitQuery.MoveNext(out var transitUid, out var transit))
        {
            if (transit.LowerMap is not { } lowerMap ||
                !_shadowMapGaps.TryGetValue(lowerMap, out var lowerGap))
            {
                continue;
            }

            CollectShadowGrids(transitUid, lowerGap, args.WorldBounds, requireAirborne: false);
        }

        if (_shadowGrids.Count == 0)
            return;

        var viewport = args.Viewport;
        var worldHandle = args.WorldHandle;

        var lightOverlay = _overlay.GetOverlay<BeforeLightTargetOverlay>();
        var bounds = lightOverlay.EnlargedBounds;
        var target = lightOverlay.GetCachedForViewport(viewport).EnlargedLightTarget;

        var lightScale = viewport.LightRenderTarget.Size / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);
        var invMatrix = target.GetWorldToLocalMatrix(eye, scale);

        worldHandle.RenderInRenderTarget(target,
            () =>
            {
                foreach (var (grid, height) in _shadowGrids)
                {
                    var alpha = MaxShadowAlpha * MathF.Exp(-ShadowHeightFalloff * height);
                    if (alpha <= 0.01f)
                        continue;

                    var color = Color.WithAlpha(Color.A * alpha);

                    var gridMatrix = _xformSystem.GetWorldMatrix(grid.Owner);
                    var matty = Matrix3x2.Multiply(gridMatrix, invMatrix);
                    worldHandle.SetTransform(matty);

                    var tileEnumerator = _mapSystem.GetTilesEnumerator(grid.Owner, grid, bounds);
                    while (tileEnumerator.MoveNext(out var tileRef))
                    {
                        var tileDef = (ContentTileDefinition) _tileDefMan[tileRef.Tile.TypeId];
                        if (tileDef.Transparent)
                            continue;

                        var local = _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize);
                        worldHandle.DrawRect(local, color);
                    }
                }
            }, null);

        worldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void CollectShadowGrids(EntityUid mapUid,
        float gap,
        Box2Rotated bounds,
        bool requireAirborne)
    {
        if (!_entManager.TryGetComponent(mapUid, out MapComponent? mapComp))
            return;

        _grids.Clear();
        _mapManager.FindGridsIntersecting(mapComp.MapId, bounds, ref _grids, approx: true, includeMap: false);

        foreach (var grid in _grids)
        {
            var localPos = 0f;
            if (_entManager.TryGetComponent(grid.Owner, out CEZPhysicsComponent? zPhys))
                localPos = Math.Clamp(zPhys.LocalPosition, 0f, 1f);

            if (requireAirborne && localPos <= 0.01f)
                continue;

            _shadowGrids.Add((grid, gap + localPos));
        }
    }
}

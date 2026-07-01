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
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _mapSystem;
    private readonly SharedTransformSystem _xformSystem;
    private readonly CESharedZLevelsSystem _zLevel;

    private List<Entity<MapGridComponent>> _grids = new();

    public Color Color = Color.Black;

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


        _grids.Clear();
        foreach (var aboveMap in _zLevel.GetAllMapsAbove((args.MapUid, zComp)))
        {
            if (_entManager.TryGetComponent(aboveMap, out MapComponent? aboveMapComp))
                _mapManager.FindGridsIntersecting(aboveMapComp.MapId, args.WorldBounds, ref _grids, approx: true, includeMap: false);
        }

        if (_grids.Count == 0)
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
                foreach (var grid in _grids)
                {
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
                        worldHandle.DrawRect(local, Color);
                    }
                }
            }, null);

        worldHandle.SetTransform(Matrix3x2.Identity);
    }
}

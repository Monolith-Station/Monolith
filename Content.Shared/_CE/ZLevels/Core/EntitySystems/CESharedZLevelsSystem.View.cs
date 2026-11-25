/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.Events;
using Content.Shared.Actions;
using Content.Shared.Maps;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] protected ITileDefinitionManager TilDefMan = null!;
    [Dependency] private SharedActionsSystem _lookUpActions = default!;
    [Dependency] private SharedPopupSystem _lookUpPopup = default!;

    private void InitializeView()
    {
        SubscribeLocalEvent<CEZLevelViewerComponent, CEToggleZLevelLookUpAction>(OnToggleLookUp);
    }

    private void OnToggleLookUp(Entity<CEZLevelViewerComponent> entity, ref CEToggleZLevelLookUpAction args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!entity.Comp.LookUp && (Transform(entity).MapUid is not { } map || !TryMapUp(map, out _)))
        {
            _lookUpPopup.PopupClient(Loc.GetString("ce-zlevel-look-up-empty"), entity, entity);
            return;
        }

        entity.Comp.LookUp = !entity.Comp.LookUp;
        DirtyField(entity, entity.Comp, nameof(CEZLevelViewerComponent.LookUp));
        _lookUpActions.SetToggled(entity.Comp.ActionEntity, entity.Comp.LookUp);
    }

    public int GetVisibleZLevelsAbove(EntityUid observer, EntityUid map)
    {
        var worldPos = _transform.GetWorldPosition(observer);
        var visible = 0;
        for (var i = 1; i <= MaxZLevelsAboveRendering; i++)
        {
            if (!TryMapOffset(map, i, out var above) || HasOpaqueTile(worldPos, above))
                break;

            visible++;
        }

        return visible;
    }

    /// <summary>
    /// Checks for an opaque tile on the map above, optionally limited to a maximum Z-depth.
    /// </summary>
    [PublicAPI]
    public bool HasOpaqueAbove(Vector2 worldPos, Entity<CEZMapComponent?> currentMap, float maxDepth = float.PositiveInfinity)
    {
        return TryMapUp(currentMap, out var above) && above.Comp.Depth <= maxDepth &&
            HasOpaqueTile(worldPos, above);
    }

    private bool HasOpaqueTile(Vector2 worldPos, EntityUid map)
    {
        if (!_map.TryFindGridAt(map, worldPos, out var gridUid, out var grid))
            return false;

        if (!_map.TryGetTileRef(gridUid, grid, worldPos, out var tileRef))
            return false;

        return !((ContentTileDefinition)TilDefMan[tileRef.Tile.TypeId]).Transparent;
    }

    public bool TryFindZShotOpening(
        EntityUid sourceMap,
        EntityUid targetMap,
        int offset,
        Vector2 from,
        Vector2 to,
        out Vector2 opening,
        bool preferOpeningAwayFromSource = false,
        float maxSourceDistanceFromOpeningEdgeTiles = float.PositiveInfinity)
    {
        opening = default;
        if (offset == 0)
            return false;

        var openingMap = offset < 0 ? sourceMap : targetMap;
        if (!_gridQuery.TryComp(openingMap, out var grid))
            return false;

        var sourceTile = preferOpeningAwayFromSource
            ? _map.WorldToTile(openingMap, grid, from)
            : default;

        var fallbackOpening = Vector2.Zero;
        var hasFallbackOpening = false;

        var maxSourceDistanceFromOpeningCenter = float.IsPositiveInfinity(maxSourceDistanceFromOpeningEdgeTiles)
            ? float.PositiveInfinity
            : grid.TileSize * (0.5f + Math.Max(0f, maxSourceDistanceFromOpeningEdgeTiles));

        var maxSourceDistanceSquared = maxSourceDistanceFromOpeningCenter * maxSourceDistanceFromOpeningCenter;
        var selectedOpening = Vector2.Zero;

        var localFrom = _map.WorldToLocal(openingMap, grid, from) / grid.TileSize;
        var localTo = _map.WorldToLocal(openingMap, grid, to) / grid.TileSize;

        var localDelta = localTo - localFrom;

        var currentTile = new Vector2i((int) MathF.Floor(localFrom.X), (int) MathF.Floor(localFrom.Y));
        var endTile = new Vector2i((int) MathF.Floor(localTo.X), (int) MathF.Floor(localTo.Y));

        var stepX = Math.Sign(localDelta.X);
        var stepY = Math.Sign(localDelta.Y);

        var tDeltaX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1f / localDelta.X);
        var tDeltaY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1f / localDelta.Y);

        var nextBoundaryX = stepX > 0 ? currentTile.X + 1f : currentTile.X;
        var nextBoundaryY = stepY > 0 ? currentTile.Y + 1f : currentTile.Y;

        var tMaxX = stepX == 0 ? float.PositiveInfinity : (nextBoundaryX - localFrom.X) / localDelta.X;
        var tMaxY = stepY == 0 ? float.PositiveInfinity : (nextBoundaryY - localFrom.Y) / localDelta.Y;

        while (true)
        {
            if (TryUseOpeningTile(currentTile))
            {
                opening = selectedOpening;
                return true;
            }

            if (currentTile == endTile)
                break;

            if (tMaxX < tMaxY)
            {
                currentTile += new Vector2i(stepX, 0);
                tMaxX += tDeltaX;
                continue;
            }

            if (tMaxY < tMaxX)
            {
                currentTile += new Vector2i(0, stepY);
                tMaxY += tDeltaY;
                continue;
            }

            currentTile += new Vector2i(stepX, stepY);
            tMaxX += tDeltaX;
            tMaxY += tDeltaY;
        }

        if (!hasFallbackOpening)
            return false;

        opening = fallbackOpening;
        return true;

        bool TryUseOpeningTile(Vector2i tile)
        {
            if (_map.TryGetTileRef(openingMap, grid, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
                return false;

            var openingCenter = _map.ToCenterCoordinates(openingMap, tile, grid).Position;
            if (Vector2.DistanceSquared(from, openingCenter) > maxSourceDistanceSquared)
                return false;

            if (preferOpeningAwayFromSource && tile == sourceTile)
            {
                if (hasFallbackOpening)
                    return false;

                fallbackOpening = openingCenter;
                hasFallbackOpening = true;

                return false;
            }

            selectedOpening = openingCenter;
            return true;
        }
    }
}

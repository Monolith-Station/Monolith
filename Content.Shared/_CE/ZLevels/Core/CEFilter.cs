/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Shared._CE.ZLevels.Core;

/// <summary>
/// CrystallEdge-specific filter factory methods that extend <see cref="Filter"/> functionality
/// for ZLevels support
/// </summary>
public static class CEFilter
{
    /// <summary>
    /// A filter with every player whose PVS overlaps this point, excluding the origin entity,
    /// plus players on adjacent z-levels who can visually see the origin's position:
    /// </summary>
    [PublicAPI]
    public static Filter ZPvsExcept(EntityUid origin, IEntityManager? entManager = null)
    {
        IoCManager.Resolve(ref entManager);

        var filter = Filter.PvsExcept(origin, entityManager: entManager);

        var zSystem = entManager.System<CESharedZLevelsSystem>();
        var transformSystem = entManager.System<SharedTransformSystem>();

        var xform = entManager.GetComponent<TransformComponent>(origin);

        var worldPos = transformSystem.GetWorldPosition(origin);

        if (xform.MapUid is not { } currentMap)
            return filter;

        var visibleMap = new List<EntityUid>();

        if (zSystem.TryMapOffset(currentMap, 1, out var mapAbove))
            visibleMap.Add(mapAbove);

        for (var i = 1; i <= CESharedZLevelsSystem.MaxZLevelsBelowRendering; i++)
        {
            if (zSystem.TryMapOffset(currentMap, -i, out var mapBelow))
                visibleMap.Add(mapBelow);
        }

        foreach (var map in visibleMap)
        {
            if (entManager.TryGetComponent<MapComponent>(map, out var mapComp))
            {
                var mapCoord = new MapCoordinates(worldPos, mapComp.MapId);
                filter.AddPlayersByPvs(mapCoord);
            }
        }

        return filter;
    }
    /// <summary>
    /// A filter with every player whose PVS overlaps this point, excluding the origin entity,
    /// plus players on adjacent z-levels who can visually see the origin's position:
    /// </summary>
    [PublicAPI]
    public static Filter ZPvs(EntityUid origin, IEntityManager? entManager = null)
    {
        IoCManager.Resolve(ref entManager);

        var filter = Filter.Pvs(origin, entityManager: entManager);

        var zSystem = entManager.System<CESharedZLevelsSystem>();
        var transformSystem = entManager.System<SharedTransformSystem>();

        var xform = entManager.GetComponent<TransformComponent>(origin);

        var worldPos = transformSystem.GetWorldPosition(origin);

        if (xform.MapUid is not { } currentMap)
            return filter;

        var visibleMap = new List<EntityUid>();

        if (zSystem.TryMapOffset(currentMap, 1, out var mapAbove))
            visibleMap.Add(mapAbove);

        for (var i = 1; i <= CESharedZLevelsSystem.MaxZLevelsBelowRendering; i++)
        {
            if (zSystem.TryMapOffset(currentMap, -i, out var mapBelow))
                visibleMap.Add(mapBelow);
        }

        foreach (var map in visibleMap)
        {
            if (entManager.TryGetComponent<MapComponent>(map, out var mapComp))
            {
                var mapCoord = new MapCoordinates(worldPos, mapComp.MapId);
                filter.AddPlayersByPvs(mapCoord);
            }
        }

        return filter;
    }
}

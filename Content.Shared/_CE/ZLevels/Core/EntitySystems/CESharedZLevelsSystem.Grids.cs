/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] private EntityQuery<CEZGridComponent> _zGridQuery = default!;
    [Dependency] private EntityQuery<CEZGridNetworkComponent> _zGridNetworkQuery = default!;

    /// <summary>
    /// Returns the z-level depth of the map containing the given grid,
    /// or null if the grid's map is not part of a z-level network.
    /// </summary>
    [PublicAPI]
    public int? TryGetGridZDepth(EntityUid gridUid)
    {
        var mapUid = Transform(gridUid).MapUid;
        return mapUid.HasValue && _zMapQuery.TryComp(mapUid.Value, out var zMap) ? zMap.Depth : null;
    }

    /// <summary>
    /// Returns the world tile coordinate for a grid tile (floor of tile-center world position).
    /// Correct for 90°-rotated, tile-snapped grids — tile centers are always at half-integer world coords.
    /// </summary>
    [PublicAPI]
    public Vector2i GridTileToWorldTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var c = _map.GridTileToWorldPos(gridUid, grid, tile);
        return new Vector2i((int)MathF.Floor(c.X), (int)MathF.Floor(c.Y));
    }

    /// <summary>
    /// Cache-first lookup: checks <see cref="CEZGridComponent.Network"/> first,
    /// falls back to a NetworkId string scan and updates the cache on a miss.
    /// </summary>
    [PublicAPI]
    public bool TryGetGridNetwork(EntityUid grid, out Entity<CEZGridNetworkComponent> network)
    {
        network = default;

        if (!_zGridQuery.TryComp(grid, out var zGridComp) || zGridComp.NetworkId == string.Empty)
            return false;

        // Fast path
        if (zGridComp.Network.IsValid() && _zGridNetworkQuery.TryComp(zGridComp.Network, out var cached))
        {
            network = (zGridComp.Network, cached);
            return true;
        }

        // Slow path — scan by NetworkId, update cache on hit
        var q = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (q.MoveNext(out var uid, out var nc))
        {
            if (nc.NetworkId != zGridComp.NetworkId)
                continue;
            zGridComp.Network = uid;
            network = (uid, nc);
            return true;
        }

        return false;
    }
}

/// <summary>
/// Raised on ZLevel grid Network Entity, when grid added or removed from network
/// </summary>
public sealed class CEZLevelGridNetworkUpdatedEvent : EntityEventArgs;

/// <summary>
/// Raised at a grid entity when it is added to a z-grid network by <see cref="CEZGridConnectorSystem"/>.
/// </summary>
[ByRefEvent]
public readonly struct CEGridAddedIntoZNetworkEvent(Entity<CEZGridNetworkComponent> network)
{
    public readonly Entity<CEZGridNetworkComponent> Network = network;
}

/// <summary>
/// Raised at a grid entity when it is removed from a z-grid network,
/// either by the recalculator or by external network deletion.
/// </summary>
[ByRefEvent]
public readonly struct CEGridRemovedFromZNetworkEvent(Entity<CEZGridNetworkComponent> network)
{
    public readonly Entity<CEZGridNetworkComponent> Network = network;
}

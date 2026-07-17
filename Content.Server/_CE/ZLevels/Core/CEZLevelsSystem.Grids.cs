/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [PublicAPI]
    public Entity<CEZGridNetworkComponent> CreateGridNetwork()
    {
        var ent  = Spawn();

        var comp = EnsureComp<CEZGridNetworkComponent>(ent);
        comp.NetworkId = Guid.NewGuid().ToString("N");
        Dirty(ent, comp);

        return (ent, comp);
    }

    [PublicAPI]
    public bool TryAddGridToNetwork(Entity<CEZGridNetworkComponent> gridNetwork, EntityUid grid)
    {
        if (!_mapGridQuery.HasComp(grid))
        {
            Log.Error($"ZGrid: {grid} is not a MapGrid.");
            return false;
        }

        if (TryGetGridNetwork(grid, out var existing))
        {
            Log.Error($"ZGrid: grid {grid} already in network {existing.Owner}.");
            return false;
        }

        gridNetwork.Comp.Grids.Add(grid);
        Dirty(gridNetwork);

        var zGridComp = EnsureComp<CEZGridComponent>(grid);
        zGridComp.NetworkId = gridNetwork.Comp.NetworkId;
        zGridComp.Network   = gridNetwork.Owner;
        Dirty(grid, zGridComp);

        var ev = new CEGridAddedIntoZNetworkEvent(gridNetwork);
        RaiseLocalEvent(grid, ref ev);

        RaiseLocalEvent(gridNetwork, new CEZLevelGridNetworkUpdatedEvent());

        return true;
    }

    [PublicAPI]
    public bool TryRemoveGridFromNetwork(EntityUid grid)
    {
        if (!TryGetGridNetwork(grid, out var gridNetwork))
            return false;

        gridNetwork.Comp.Grids.Remove(grid);
        RemComp<CEZGridComponent>(grid);

        if (!TerminatingOrDeleted(gridNetwork.Owner))
            Dirty(gridNetwork);

        var ev = new CEGridRemovedFromZNetworkEvent(gridNetwork);
        RaiseLocalEvent(grid, ref ev);

        if (gridNetwork.Comp.Grids.Count == 0 && !TerminatingOrDeleted(gridNetwork.Owner))
            QueueDel(gridNetwork);
        else
        {
            RaiseLocalEvent(gridNetwork, new CEZLevelGridNetworkUpdatedEvent());
        }

        return true;
    }

    /// <summary>
    /// Explicit teardown: removes every grid (raising <see cref="CEGridRemovedFromZNetworkEvent"/> per grid)
    /// and queues the manager for deletion.
    /// </summary>
    [PublicAPI]
    public void DeleteGridNetwork(Entity<CEZGridNetworkComponent> network)
    {
        // TryRemoveGridFromNetwork mutates Grids, so iterate a snapshot.
        foreach (var grid in network.Comp.Grids.ToList())
        {
            TryRemoveGridFromNetwork(grid);
        }

        if (!TerminatingOrDeleted(network.Owner))
            QueueDel(network);
    }
}

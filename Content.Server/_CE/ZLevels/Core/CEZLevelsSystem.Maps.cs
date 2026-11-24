/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Server._CE.ZLevels.PVS;
using Content.Server._Mono.Planets;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._FarHorizons.StarSystem;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    /// <summary>
    /// Creates a new zLevel Map Network entity
    /// </summary>
    [PublicAPI]
    public Entity<CEZMapNetworkComponent> CreateMapNetwork(ComponentRegistry? components = null)
    {
        var ent = Spawn();

        var zLevel = EnsureComp<CEZMapNetworkComponent>(ent);
        EnsureComp<CEPvsOverrideComponent>(ent);

        zLevel.Components = components ?? new ComponentRegistry();

        return (ent, zLevel);
    }

    /// <summary>
    /// Attempts to add the specified maps to the zNetwork network at the specified depths
    /// </summary>
    [PublicAPI]
    public bool TryAddMapsIntoNetwork(Entity<CEZMapNetworkComponent> network, Dictionary<EntityUid, int> maps,
        bool applyComponents = true)
    {
        var depths = new HashSet<int>(network.Comp.ZLevels.Keys);
        foreach (var (mapUid, depth) in maps)
        {
            if (!HasComp<MapComponent>(mapUid) || TerminatingOrDeleted(mapUid) ||
                EntityManager.IsQueuedForDeletion(mapUid) || HasComp<CEZTransitMapComponent>(mapUid) ||
                HasComp<PlanetTransitMapComponent>(mapUid) ||
                (TryComp<CEZMapComponent>(mapUid, out var level) && level.NetworkUid.IsValid()) ||
                network.Comp.ZLevelByEntity.ContainsKey(mapUid) || !depths.Add(depth))
            {
                Log.Warning($"Cannot add map {mapUid} at depth {depth} to Z-level network {network}.");
                return false;
            }
        }

        if (maps.Count == 0)
            return true;

        // The depth cache has one slot per depth, including gaps.
        if ((long) depths.Max() - depths.Min() >= 4096 || depths.Contains(int.MinValue) || depths.Contains(int.MaxValue))
            return false;

        foreach (var (mapUid, depth) in maps)
        {
            network.Comp.ZLevels[depth] = mapUid;
            network.Comp.ZLevelByEntity[mapUid] = depth;

            var levelMapComponent = EnsureComp<CEZMapComponent>(mapUid);
            levelMapComponent.Depth = depth;
            levelMapComponent.NetworkUid = network;
        }

        foreach (var (mapUid, depth) in maps)
        {
            var level = Comp<CEZMapComponent>(mapUid);
            if (network.Comp.ZLevels.GetValueOrDefault(depth + 1) is { } aboveUid &&
                TryComp<CEZMapComponent>(aboveUid, out var above))
            {
                level.MapAbove = aboveUid;
                above.MapBelow = mapUid;
                Dirty(aboveUid, above);
            }

            if (network.Comp.ZLevels.GetValueOrDefault(depth - 1) is { } belowUid &&
                TryComp<CEZMapComponent>(belowUid, out var below))
            {
                level.MapBelow = belowUid;
                below.MapAbove = mapUid;
                Dirty(belowUid, below);
            }

            Dirty(mapUid, level);
        }

        RebuildMapDepthCache(network);
        foreach (var (mapUid, depth) in maps)
        {
            var ev = new CEMapAddedIntoZNetworkEvent(network, depth, applyComponents);
            RaiseLocalEvent(mapUid, ref ev);
            SweepMapGridsForZPhysics(mapUid);
        }

        MapNetworkChanged(network);
        return true;
    }

    /// <summary>Detaches a map without deleting it. Managed planet layers and live transit anchors cannot be detached.</summary>
    [PublicAPI]
    public bool TryRemoveMapFromNetwork(Entity<CEZMapNetworkComponent> network, EntityUid mapUid)
    {
        if (HasComp<PlanetMapGenerationComponent>(network) ||
            !network.Comp.ZLevelByEntity.TryGetValue(mapUid, out var depth) || HasTransitMapReference(mapUid))
            return false;

        RemoveMapFromNetwork(network, mapUid, depth);
        return true;
    }

    private void OnZMapTerminating(Entity<CEZMapComponent> ent, ref EntityTerminatingEvent args)
    {
        var networkUid = ent.Comp.NetworkUid;
        if (TerminatingOrDeleted(networkUid) ||
            !TryComp<CEZMapNetworkComponent>(networkUid, out var network) ||
            !network.ZLevelByEntity.TryGetValue(ent.Owner, out var depth))
        {
            return;
        }

        RemoveMapFromNetwork((networkUid, network), ent.Owner, depth);
    }

    private void RemoveMapFromNetwork(Entity<CEZMapNetworkComponent> network, EntityUid mapUid, int depth)
    {
        network.Comp.ZLevels.Remove(depth);
        network.Comp.ZLevelByEntity.Remove(mapUid);

        if (network.Comp.ZLevels.GetValueOrDefault(depth + 1) is { } aboveUid &&
            TryComp<CEZMapComponent>(aboveUid, out var above))
        {
            above.MapBelow = null;
            Dirty(aboveUid, above);
        }

        if (network.Comp.ZLevels.GetValueOrDefault(depth - 1) is { } belowUid &&
            TryComp<CEZMapComponent>(belowUid, out var below))
        {
            below.MapAbove = null;
            Dirty(belowUid, below);
        }

        RebuildMapDepthCache(network);
        if (!TerminatingOrDeleted(mapUid))
        {
            RemComp<CEZMapComponent>(mapUid);
            SweepMapGridsForZPhysics(mapUid);
        }

        MapNetworkChanged(network, (mapUid, depth));
    }

    private void MapNetworkChanged(Entity<CEZMapNetworkComponent> network, (EntityUid Map, int Depth)? removedMap = null)
    {
        RefreshTransitMapLinks(network, removedMap);
        QueueAllViewerUpdates();
        if (!EntityManager.IsQueuedForDeletion(network))
            RaiseLocalEvent(network, new CEZLevelMapNetworkUpdatedEvent());
    }

    /// <summary>
    /// Returns the map entity at a specific depth within a z-network, or false if none exists.
    /// </summary>
    [PublicAPI]
    public bool TryGetMapAtDepth(Entity<CEZMapNetworkComponent?> network, int depth, out EntityUid mapUid)
    {
        mapUid = EntityUid.Invalid;

        if (!Resolve(network, ref network.Comp, false) ||
            !network.Comp.ZLevels.TryGetValue(depth, out var uid) ||
            uid is not { } validUid)
            return false;

        mapUid = validUid;
        return true;
    }

    /// <summary>
    /// Deletes a map z-network: queues deletion of all maps in the network, then the network entity itself.
    /// </summary>
    [PublicAPI]
    public void DeleteMapNetwork(EntityUid networkUid)
    {
        if (!TryComp<CEZMapNetworkComponent>(networkUid, out var zNet))
        {
            Log.Error($"CEZLevelsSystem: entity {networkUid} does not have CEZLevelsNetworkComponent.");
            return;
        }

        foreach (var (_, mapUid) in zNet.ZLevels)
        {
            if (mapUid != null)
                QueueDel(mapUid.Value);
        }

        QueueDel(networkUid);
    }

    private void RebuildMapDepthCache(Entity<CEZMapNetworkComponent> network)
    {
        var comp = network.Comp;
        var list = comp.SortedZLevels;
        list.Clear();
        comp.SortedMin = 0;
        comp.SortedMax = 0;
        if (comp.ZLevels.Count > 0)
        {
            comp.SortedMin = comp.ZLevels.Keys.Min();
            comp.SortedMax = comp.ZLevels.Keys.Max();
            list.AddRange(Enumerable.Repeat(EntityUid.Invalid, comp.SortedMax - comp.SortedMin + 1));
            foreach (var (depth, uid) in comp.ZLevels)
                list[depth - comp.SortedMin] = uid ?? EntityUid.Invalid;
        }

        Dirty(network);
    }
}

/// <summary>
/// Called on ZLevel Network Entity, when maps added or removed from network
/// </summary>
public sealed class CEZLevelMapNetworkUpdatedEvent : EntityEventArgs;

/// <summary>
/// Called on map, when it added to ZNetwork
/// </summary>
[ByRefEvent]
public readonly struct CEMapAddedIntoZNetworkEvent(Entity<CEZMapNetworkComponent> network, int depth, bool applyComponents)
{
    public readonly Entity<CEZMapNetworkComponent> Network = network;
    public readonly int Depth = depth;
    public readonly bool ApplyComponents = applyComponents;
}

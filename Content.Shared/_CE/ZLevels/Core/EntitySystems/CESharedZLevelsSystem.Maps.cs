/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Chasm;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    /// <summary>
    /// Checks whether the map is in the zLevels network. If so, returns true and the current depth + Entity of the current zLevels network.
    /// </summary>
    [PublicAPI]
    public bool TryGetMapNetwork(EntityUid mapUid, out Entity<CEZMapNetworkComponent> zLevel)
    {
        zLevel = default;
        if (!_zMapQuery.TryComp(mapUid, out var zMap))
            return false;

        var networkUid = zMap.NetworkUid;
        if (TerminatingOrDeleted(networkUid))
        {
            Log.Warning($"Trying access to terminated z-network, map: {mapUid}, outdated network uid: {networkUid}");
            return false;
        }

        if (!_zNetworkQuery.TryComp(networkUid, out var zNetworkComponent))
        {
            Log.Warning($"Trying access to z-network without component??? WHY?! map: {mapUid}, network uid: {networkUid}");
            return false;
        }

        zLevel = (networkUid, zNetworkComponent);
        return true;
    }

    [PublicAPI]
    public bool TryMapOffset(Entity<CEZMapComponent?> entity, int offset, out Entity<CEZMapComponent> output)
    {
        return TryMapOffset(entity, offset, out output, out _);
    }

    [PublicAPI]
    public bool TryMapOffset(Entity<CEZMapComponent?> entity, int offset, out Entity<CEZMapComponent> output, [NotNullWhen(true)] out MapComponent? mapComponent)
    {
        output = default;
        mapComponent = null;

        if (MapOffset(entity, offset) is not { } result)
            return false;

        if (!TryComp(result, out mapComponent))
            return false;

        output = result;
        return true;
    }

    [PublicAPI]
    public bool TryMapUp(Entity<CEZMapComponent?> inputMapUid, out Entity<CEZMapComponent> aboveMapUid) =>
        TryMapOffset(inputMapUid, 1, out aboveMapUid);

    [PublicAPI]
    public bool TryMapDown(Entity<CEZMapComponent?> inputMapUid, out Entity<CEZMapComponent> belowMapUid) =>
        TryMapOffset(inputMapUid, -1, out belowMapUid);

    /// <summary>
    /// Returns a list of all maps above the specified map. The closest map at the top is returned first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsAbove(Entity<CEZMapComponent> entity)
    {
        if (!_zNetworkQuery.TryComp(entity.Comp.NetworkUid, out var network) || entity.Comp.Depth >= network.SortedMax)
            return new List<EntityUid>();

        var startIndex = entity.Comp.Depth < network.SortedMin ? 0 : entity.Comp.Depth - network.SortedMin + 1;
        var estimatedCount = network.SortedZLevels.Count - startIndex;
        var result = new List<EntityUid>(estimatedCount);
        var zLevels = network.SortedZLevels;

        for (var i = startIndex; i < zLevels.Count; i++)
        {
            var uid = zLevels[i];
            if (uid == EntityUid.Invalid)
                continue;

            if (_zMapQuery.HasComp(uid))
                result.Add(uid);
        }

        return result;
    }

    /// <summary>
    /// Returns a list of all maps above the specified map. The closest map at the top is returned first.
    /// </summary>
    [PublicAPI]
    public void GetAllMapsAbove(Entity<CEZMapComponent> entity, List<EntityUid> result)
    {
        result.Clear();

        if (!_zNetworkQuery.TryComp(entity.Comp.NetworkUid, out var network) || entity.Comp.Depth >= network.SortedMax)
            return;

        var startIndex = entity.Comp.Depth < network.SortedMin ? 0 : entity.Comp.Depth - network.SortedMin + 1;
        var zLevels = network.SortedZLevels;

        for (var i = startIndex; i < zLevels.Count; i++)
        {
            var uid = zLevels[i];

            if (uid.IsValid() && _zMapQuery.HasComp(uid))
                result.Add(uid);
        }
    }

    /// <summary>
    /// Returns a list of all maps below the specified map. The closest map at the bottom is returned first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsBelow(Entity<CEZMapComponent> entity)
    {
        if (!_zNetworkQuery.TryComp(entity.Comp.NetworkUid, out var network) || entity.Comp.Depth <= network.SortedMin)
            return new List<EntityUid>();

        var endIndex = entity.Comp.Depth - network.SortedMin;
        var result = new List<EntityUid>(endIndex);
        var zLevels = network.SortedZLevels;

        // Iterate backwards for nearest-first ordering.
        for (var i = endIndex - 1; i >= 0; i--)
        {
            var uid = zLevels[i];
            if (uid == EntityUid.Invalid)
                continue;

            if (_zMapQuery.HasComp(uid))
                result.Add(uid);
        }

        return result;
    }

    private Entity<CEZMapComponent>? MapOffset(Entity<CEZMapComponent?> entity, int offset)
    {
        // Transit map fuckery! Woo!
        if (offset != 0 && entity.Comp is null && !_zMapQuery.HasComp(entity) &&
            TryComp<CEZTransitMapComponent>(entity, out var transit))
        {
            var anchor = offset < 0 ? transit.LowerMap : transit.UpperMap;
            if (anchor is not { } anchorUid || TerminatingOrDeleted(anchorUid))
                return null;

            var remaining = offset < 0 ? offset + 1 : offset - 1;
            if (remaining == 0)
            {
                return _zMapQuery.TryComp(anchorUid, out var anchorComp)
                    ? (anchorUid, anchorComp)
                    : null;
            }

            return MapOffset(new Entity<CEZMapComponent?>(anchorUid, null), remaining);
        }

        if (!Resolve(entity, ref entity.Comp, false))
            return null;

        var target = offset switch
        {
            1 => entity.Comp.MapAbove,
            -1 => entity.Comp.MapBelow,
            _ => null,
        };

        if (target is not null && _zMapQuery.TryComp(target.Value, out var component))
            return (target.Value, component);

        if (!_zNetworkQuery.TryComp(entity.Comp.NetworkUid, out var network))
            return null;

        if (!network.ZLevels.TryGetValue(entity.Comp.Depth + offset, out var targetId))
            return null;

        return _zMapQuery.TryComp(targetId, out var comp)
            ? (targetId.Value, comp)
            : null;
    }
}

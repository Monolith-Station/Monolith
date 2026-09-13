/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Mapping.Prototypes;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.ZLevels.Mapping;

/// <summary>
/// Spawns multi-deck ships (<see cref="CEZGridStackPrototype"/>) across the layers of a
/// z-map network. A deck's z-layer is set by which depth-map it loads onto — grids carry no
/// layer of their own — so each deck goes onto baseDepth + its index, all at one world
/// position. The connector pillars mapped into the decks knit them into a z-grid network.
/// </summary>
public sealed partial class CEZGridStackSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private CEZLevelsSystem _zLevels = default!;

    /// <summary>
    /// Loads every deck of <paramref name="protoId"/> onto <paramref name="network"/>, deck i
    /// on the layer at <paramref name="baseDepth"/> + i and offset to <paramref name="worldPos"/>.
    /// All-or-nothing: verifies every target layer exists first, and unwinds any decks it
    /// loaded if a later one fails, so a partial ship is never left behind.
    /// </summary>
    public bool TrySpawnGridStack(
        ProtoId<CEZGridStackPrototype> protoId,
        Entity<CEZMapNetworkComponent> network,
        int baseDepth,
        Vector2 worldPos,
        List<EntityUid>? spawned = null)
    {
        if (!_proto.TryIndex(protoId, out var proto) || proto.Decks.Count == 0)
            return false;

        // Verify every target layer exists before loading anything.
        for (var i = 0; i < proto.Decks.Count; i++)
        {
            if (!_zLevels.TryGetMapAtDepth((network.Owner, network.Comp), baseDepth + i, out _))
            {
                Log.Error($"CEZGridStack '{protoId}': network {network.Owner} has no layer at depth {baseDepth + i}.");
                return false;
            }
        }

        var loaded = new List<EntityUid>();
        for (var i = 0; i < proto.Decks.Count; i++)
        {
            _zLevels.TryGetMapAtDepth((network.Owner, network.Comp), baseDepth + i, out var mapUid);

            if (!TryComp<MapComponent>(mapUid, out var mapComp)
                || !_mapLoader.TryLoadGrid(mapComp.MapId, proto.Decks[i], out var grid,
                       new DeserializationOptions { InitializeMaps = true }, offset: worldPos))
            {
                Log.Error($"CEZGridStack '{protoId}': failed to load deck {i} ({proto.Decks[i]}); unwinding.");
                foreach (var uid in loaded)
                    QueueDel(uid);
                return false;
            }

            loaded.Add(grid.Value.Owner);
            spawned?.Add(grid.Value.Owner);
        }

        return true;
    }
}

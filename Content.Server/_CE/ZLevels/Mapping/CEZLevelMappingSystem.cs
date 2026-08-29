/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Mapping.Prototypes;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.ZLevels.Mapping;

public sealed partial class CEZLevelMappingSystem : EntitySystem
{
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZMapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEZMapComponent, CEMapAddedIntoZNetworkEvent>(OnAddedIntoZNetwork);
    }

    private void OnAddedIntoZNetwork(Entity<CEZMapComponent> ent, ref CEMapAddedIntoZNetworkEvent args)
    {
        if (_map.IsInitialized(ent))
            EntityManager.AddComponents(ent, args.Network.Comp.Components);
        else
        {
            var hasInitializedMaps = false;
            foreach (var existingMapUid in args.Network.Comp.ZLevels.Values)
            {
                if (existingMapUid.HasValue && _map.IsInitialized(existingMapUid.Value))
                {
                    hasInitializedMaps = true;
                    break;
                }
            }

            if (hasInitializedMaps)
                _map.InitializeMap(ent.Owner);
        }
    }

    private void OnMapInit(Entity<CEZMapComponent> ent, ref MapInitEvent args)
    {
        if (!_zLevels.TryGetMapNetwork(ent, out var network))
            return;

        EntityManager.AddComponents(ent, network.Comp.Components);
    }

    public bool TryLoadNetwork(ProtoId<CEZLevelMapPrototype> id,
        string name,
        out Entity<CEZMapNetworkComponent> network)
    {
        network = default;
        if (!_proto.TryIndex(id, out var proto))
            return false;

        var created = new Dictionary<EntityUid, int>();
        for (var depth = 0; depth < proto.Maps.Count; depth++)
        {
            if (!_mapLoader.TryLoadMap(proto.Maps[depth], out var map, out _))
            {
                foreach (var uid in created.Keys)
                    QueueDel(uid);
                return false;
            }

            created.Add(map.Value, depth);
            _meta.SetEntityName(map.Value, $"{name} [{depth}]");
        }

        network = _zLevels.CreateMapNetwork(proto.Components);
        _meta.SetEntityName(network, $"{name} surface");

        if (!_zLevels.TryAddMapsIntoNetwork(network, created))
        {
            _zLevels.DeleteMapNetwork(network);
            network = default;
            return false;
        }

        _zLevels.InitializeZNetwork(network);
        return true;
    }
}

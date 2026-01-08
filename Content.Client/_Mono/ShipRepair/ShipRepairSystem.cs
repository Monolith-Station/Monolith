using Content.Client.IconSmoothing;
using Content.Shared._Mono.ForceParent;
using Content.Shared._Mono.ShipRepair;
using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Client._Mono.ShipRepair;

public sealed partial class ShipRepairSystem : SharedShipRepairSystem
{
    [Dependency] private readonly ForceParentSystem _parent = default!;
    [Dependency] private readonly IconSmoothSystem _smooth = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    // so Update() is less evil
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<ShipRepairDataComponent> _dataQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<ShipRepairToolComponent> _toolQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RepairEntityMessage>(OnRepairMessage);

        _dataQuery = GetEntityQuery<ShipRepairDataComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _toolQuery = GetEntityQuery<ShipRepairToolComponent>();

        InitGhosts();
    }

    private void OnRepairMessage(RepairEntityMessage args)
    {
        var grid = GetEntity(args.Grid);
        if (TerminatingOrDeleted(grid)
            || !TryComp<ShipRepairDataComponent>(grid, out var data)
            || !TryGetChunk(data, args.Indices, out var chunk)
        )
            return;

        if (!chunk.Entities.ContainsKey(args.SpecId))
            Log.Warning($"Tried to sync repaired entity at {args.Indices} on grid {ToPrettyString(grid)}, but we did not have this entity prior.");

        chunk.Entities[args.SpecId] = args.NewSpec;
    }
}

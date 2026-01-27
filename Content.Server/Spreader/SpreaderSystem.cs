using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Shared.Atmos;
using Content.Shared.Maps;
using Content.Shared.Spreader;
using Content.Shared.Tag;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Robust.Shared.Timing;

namespace Content.Server.Spreader;

// Mono - system heavily changed
/// <summary>
/// Handles generic spreading logic, where one anchored entity spreads to neighboring tiles.
/// </summary>
public sealed class SpreaderSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    /// <summary>
    /// Cached maximum number of updates per spreader prototype. This is applied per-grid.
    /// </summary>
    private Dictionary<ProtoId<EdgeSpreaderPrototype>, int> _prototypeUpdates = new();
    private Dictionary<ProtoId<EdgeSpreaderPrototype>, bool> _prototypeSpacedSpread = new();

    private EntityQuery<ActiveEdgeSpreaderComponent> _activeQuery;
    private EntityQuery<AirtightComponent> _airtightQuery;
    private EntityQuery<DockingComponent> _dockQuery;
    private EntityQuery<MapGridComponent> _mapGridQuery;
    private EntityQuery<EdgeSpreaderComponent> _spreaderQuery;
    private EntityQuery<SpreaderGridComponent> _spreaderGridQuery;

    private AtmosDirection[] _atmosDirections = [AtmosDirection.North, AtmosDirection.East, AtmosDirection.South, AtmosDirection.West];

    // Mono - Caps spreadersystem to not run excessively.
    [ViewVariables] private readonly TimeSpan _maximumProcessTime = TimeSpan.FromMilliseconds(5);

    private static readonly ProtoId<TagPrototype> IgnoredTag = "SpreaderIgnore";

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<AirtightChanged>(OnAirtightChanged);
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);

        SubscribeLocalEvent<EdgeSpreaderComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<ActiveEdgeSpreaderComponent, MapInitEvent>(OnActiveInit);
        SetupPrototypes();

        _activeQuery = GetEntityQuery<ActiveEdgeSpreaderComponent>();
        _airtightQuery = GetEntityQuery<AirtightComponent>();
        _dockQuery = GetEntityQuery<DockingComponent>();
        _mapGridQuery = GetEntityQuery<MapGridComponent>();
        _spreaderQuery = GetEntityQuery<EdgeSpreaderComponent>();
        _spreaderGridQuery = GetEntityQuery<SpreaderGridComponent>();
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<EdgeSpreaderPrototype>())
            SetupPrototypes();
    }

    private void SetupPrototypes()
    {
        _prototypeUpdates.Clear();
        _prototypeSpacedSpread.Clear();

        foreach (var proto in _prototype.EnumeratePrototypes<EdgeSpreaderPrototype>())
        {
            _prototypeUpdates.Add(proto.ID, proto.UpdatesPerSecond);
            _prototypeSpacedSpread.Add(proto.ID, !proto.PreventSpreadOnSpaced);
        }

        // regenerate update queues
        var query = EntityQueryEnumerator<SpreaderGridComponent>();
        while (query.MoveNext(out var uid, out var spreadGrid))
            InitSpreaderGrid(uid);
    }

    private void OnAirtightChanged(ref AirtightChanged ev)
    {
        ActivateSpreadableNeighbors(ev.Entity, ev.Position);
    }

    private void OnGridInit(GridInitializeEvent ev)
    {
        InitSpreaderGrid(ev.EntityUid);
    }

    private void OnTerminating(Entity<EdgeSpreaderComponent> entity, ref EntityTerminatingEvent args)
    {
        ActivateSpreadableNeighbors(entity);
    }

    private void OnActiveInit(Entity<ActiveEdgeSpreaderComponent> entity, ref MapInitEvent args)
    {
        InitSpreader(entity);
    }

    private void InitSpreader(EntityUid spreader)
    {
        var xform = Transform(spreader);
        if (_spreaderGridQuery.TryComp(xform.GridUid, out var spreaderGrid)
            && _spreaderQuery.TryComp(spreader, out var comp))
        {
            spreaderGrid.SpreadQueues[comp.Id].Enqueue((spreader, comp));
        }
    }

    private void InitSpreaderGrid(EntityUid uid)
    {
        if (!_mapGridQuery.TryComp(uid, out var mapGrid))
            return;

        var spreaderGrid = EnsureComp<SpreaderGridComponent>(uid);
        spreaderGrid.SpreadQueues.Clear();
        foreach (var key in _prototypeUpdates.Keys)
            spreaderGrid.SpreadQueues.Add(key, new());

        var spreaders = new HashSet<Entity<ActiveEdgeSpreaderComponent>>();
        _lookup.GetLocalEntitiesIntersecting(uid, mapGrid.LocalAABB, spreaders);
        foreach (var ent in spreaders)
        {
            if (!_spreaderQuery.TryComp(ent, out var spreader))
                continue;

            spreaderGrid.SpreadQueues[spreader.Id].Enqueue((ent, spreader));
        }
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        var startTime = _timing.RealTime;

        // Check which grids are valid for spreading
        var spreadGrids = EntityQueryEnumerator<SpreaderGridComponent, MapGridComponent>();

        while (spreadGrids.MoveNext(out var gridUid, out var spreaderGrid, out var mapGrid))
        {
            // abort on time limit exceeded
            if (_timing.RealTime - startTime > _maximumProcessTime)
                break;

            spreaderGrid.UpdateAccumulator += frameTime;
            if (spreaderGrid.UpdateAccumulator < spreaderGrid.UpdateSpacing)
                continue;
            spreaderGrid.UpdateAccumulator -= spreaderGrid.UpdateSpacing;

            foreach (var (spreadProto, spreadQueue) in spreaderGrid.SpreadQueues)
            {
                var spacedSpread = _prototypeSpacedSpread[spreadProto];
                var updates = _prototypeUpdates[spreadProto];
                var count = spreadQueue.Count;
                for (var i = 0; i < count && updates > 0; i++)
                {
                    var ent = spreadQueue.Dequeue();

                    if (TerminatingOrDeleted(ent) || !_activeQuery.HasComp(ent))
                        continue;

                    var xform = Transform(ent);
                    if (xform.GridUid != gridUid)
                    {
                        InitSpreader(ent);
                        continue;
                    }

                    // try to update the specified amount of active spreaders
                    Spread(ent, (gridUid, mapGrid), xform, spacedSpread, ref updates);

                    spreadQueue.Enqueue(ent); // requeue it
                }
            }
        }
    }

    private void Spread(Entity<EdgeSpreaderComponent> ent, Entity<MapGridComponent> grid, TransformComponent xform, bool spreadSpaced, ref int updates)
    {
        GetNeighbors(ent, grid, xform, spreadSpaced, out var freeTiles, out var neighbors);

        var ev = new SpreadNeighborsEvent()
        {
            NeighborFreeTiles = freeTiles,
            Neighbors = neighbors,
            Updates = updates
        };

        RaiseLocalEvent(ent, ref ev);
        updates = ev.Updates;
    }

    /// <summary>
    /// Gets the neighboring node data for the specified entity and the specified node group.
    /// </summary>
    private void GetNeighbors(Entity<EdgeSpreaderComponent> ent, Entity<MapGridComponent> grid, TransformComponent xform, bool spreadSpaced, out List<(MapGridComponent, TileRef)> freeTiles, out List<EntityUid> neighbors)
    {
        freeTiles = [];
        neighbors = [];
        // TODO remove occupiedTiles -- its currently unused and just slows this method down.

        var tile = _map.TileIndicesFor(grid, xform.Coordinates);
        var blockedAtmosDirs = AtmosDirection.Invalid;

        // Due to docking ports they may not necessarily be opposite directions.
        var neighborTiles = new ValueList<(EntityUid entity, MapGridComponent grid, Vector2i Indices, AtmosDirection OtherDir, AtmosDirection OurDir)>();

        // Check if anything on our own tile blocking that direction.
        var ourEnts = _map.GetAnchoredEntitiesEnumerator(grid, grid, tile);
        while (ourEnts.MoveNext(out var anchUid))
        {
            // Spread via docks in a special-case.
            if (_dockQuery.TryComp(anchUid, out var dock)
                && dock.Docked
                && dock.DockedWith != null
                && !TerminatingOrDeleted(dock.DockedWith))
            {
                var dockedXform = Transform(dock.DockedWith.Value);
                var ourXform = Transform(anchUid.Value);
                if (dockedXform.GridUid is { } dockedGrid && _mapGridQuery.TryComp(dockedGrid, out var dockedGridComp))
                    neighborTiles.Add(
                        (dockedGrid, dockedGridComp,
                        _map.CoordinatesToTile(dockedGrid, dockedGridComp, dockedXform.Coordinates),
                        ourXform.LocalRotation.ToAtmosDirection(),
                        dockedXform.LocalRotation.ToAtmosDirection()));
            }

            // If we're on a blocked tile work out which directions we can go.
            if (!_airtightQuery.TryComp(anchUid, out var airtight)
                || !airtight.AirBlocked
                || _tag.HasTag(anchUid.Value, IgnoredTag)
            )
                continue;

            blockedAtmosDirs |= airtight.AirBlockedDirection;
        }

        // Add the normal neighbors.
        for (var i = 0; i < 4; i++)
        {
            var atmosDir = (AtmosDirection)(1 << i);
            var neighborPos = tile.Offset(atmosDir);
            neighborTiles.Add((grid, grid, neighborPos, atmosDir, i.ToOppositeDir()));
        }

        foreach (var (neighborEnt, neighborGrid, neighborPos, ourAtmosDir, otherAtmosDir) in neighborTiles)
        {
            // This tile is blocked to that direction.
            if ((blockedAtmosDirs & ourAtmosDir) != 0x0)
                continue;

            if (!_map.TryGetTileRef(neighborEnt, neighborGrid, neighborPos, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            if (!spreadSpaced && tileRef.Tile.IsSpace())
                continue;

            var directionEnumerator = _map.GetAnchoredEntitiesEnumerator(neighborEnt, neighborGrid, neighborPos);
            var occupied = false;

            while (directionEnumerator.MoveNext(out var anchEnt))
            {
                if (_spreaderQuery.TryComp(anchEnt, out var other)
                    && other.Id == ent.Comp.Id)
                {
                    neighbors.Add(anchEnt.Value);
                    occupied = true;
                }

                if (!_airtightQuery.TryComp(anchEnt, out var airtight)
                    || !airtight.AirBlocked
                    || _tag.HasTag(anchEnt.Value, IgnoredTag)
                )
                    continue;

                if ((airtight.AirBlockedDirection & otherAtmosDir) != 0)
                    occupied = true;
            }

            if (!occupied)
                freeTiles.Add((neighborGrid, tileRef));
        }
    }

    /// <summary>
    /// This function activates all spreaders that are adjacent to a given entity. This also activates other spreaders
    /// on the same tile as the current entity (for thin airtight entities like windoors).
    /// </summary>
    public void ActivateSpreadableNeighbors(EntityUid uid, (EntityUid Grid, Vector2i Tile)? position = null)
    {
        Vector2i tile;
        EntityUid gridUid;
        MapGridComponent? grid;

        if (position == null)
        {
            var transform = Transform(uid);
            if (!_mapGridQuery.TryComp(transform.GridUid, out grid) || TerminatingOrDeleted(transform.GridUid.Value))
                return;

            tile = _map.TileIndicesFor(transform.GridUid.Value, grid, transform.Coordinates);
            gridUid = transform.GridUid.Value;
        }
        else
        {
            if (!_mapGridQuery.TryComp(position.Value.Grid, out grid))
                return;
            (gridUid, tile) = position.Value;
        }

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var entity))
        {
            DebugTools.Assert(Transform(entity.Value).Anchored);
            if (_spreaderQuery.HasComponent(entity) && !TerminatingOrDeleted(entity.Value))
                EnsureComp<ActiveEdgeSpreaderComponent>(entity.Value);
        }

        foreach (var direction in _atmosDirections)
        {
            var adjacentTile = SharedMapSystem.GetDirection(tile, direction.ToDirection());
            anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, adjacentTile);

            while (anchored.MoveNext(out var entity))
            {
                DebugTools.Assert(Transform(entity.Value).Anchored);
                if (_spreaderQuery.HasComponent(entity) && !TerminatingOrDeleted(entity.Value))
                    EnsureComp<ActiveEdgeSpreaderComponent>(entity.Value);
            }
        }
    }

    public bool RequiresFloorToSpread(EntProtoId<EdgeSpreaderComponent> spreader)
    {
        if (!_prototype.Index(spreader).TryGetComponent<EdgeSpreaderComponent>(out var spreaderComp, EntityManager.ComponentFactory))
            return false;

        return _prototype.Index(spreaderComp.Id).PreventSpreadOnSpaced;
    }
}

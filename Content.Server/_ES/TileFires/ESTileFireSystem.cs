using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Spreader;
using Content.Shared._ES.TileFires;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Maps;
using Content.Shared.Random.Helpers;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ES.TileFires;

/// <summary>
///     Server-side logic for tile fire growth logic, e.g. stages, requiring oxygen, etc.
///     Also spawning logic.
/// </summary>
public sealed class ESTileFireSystem : ESSharedTileFireSystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static EntProtoId _stage1Fire = "ESTileFire";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ESTileFireComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ESTileFireComponent, SpreadNeighborsEvent>(OnSpreadNeighbors);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // handle smoldering behavior for tilefires
        // everything else is generally handled by flammable
        var query = EntityQueryEnumerator<ESTileFireComponent, FlammableComponent>();
        while (query.MoveNext(out var uid, out var tilefire, out var flammable))
        {
            if (_timing.CurTime < tilefire.SmolderTime)
                continue;

            if (_random.Prob(tilefire.SmolderDeleteFireChance))
            {
                QueueDel(uid);
                continue;
            }

            // lower it to a random lower stage fire and disable growing in strength
            var randomDivisor = _random.Next(2, 7);
            _flammable.SetFireStacks(uid, flammable.FireStacks / randomDivisor, flammable);
            flammable.FirestackFade = 0f;
            Dirty(uid, flammable);

            // remove comp so it cant smolder again + cant spread + doesnt get queried
            RemCompDeferred<ESTileFireComponent>(uid);
        }
    }

    #region Events
    private void OnMapInit(Entity<ESTileFireComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid))
            return;

        var tile = MapSys.GetTileRef((grid, mapGrid), xform.Coordinates);
        _atmos.TryAddBurntDecalsToTile(grid, tile.GridIndices, _random.Next(1, 3));

        ent.Comp.SmolderTime = _timing.CurTime + _random.Next(ent.Comp.MinSmolderTime, ent.Comp.MaxSmolderTime);
    }

    private void OnSpreadNeighbors(Entity<ESTileFireComponent> ent, ref SpreadNeighborsEvent args)
    {
        if (!TryComp<FlammableComponent>(ent, out var flammable))
            return;

        if (args.NeighborFreeTiles.Count == 0)
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(ent);
            return;
        }

        if (!_random.Prob(ent.Comp.BaseSpreadChance))
            return;

        // random alteration to firestacks required for variance
        if (flammable.FireStacks < ent.Comp.MinFirestacksToSpread * _random.NextFloat(0.75f, 1.25f))
            return;

        // Score neighboring tiles based on criteria, then do a weighted pick to spread
        Dictionary<EntityCoordinates, float> weights = new(args.NeighborFreeTiles.Count);
        foreach (var neighbor in args.NeighborFreeTiles)
        {
            // not updating the spreader api to get rid of this .owner sorry too many breakchanges for me
            var grid = neighbor.Grid.Owner;
            if (!TryComp<GridAtmosphereComponent>(grid, out var gridComp))
                continue;

            var tileDef = _turf.GetContentTileDefinition(neighbor.Tile);
            if (tileDef.Flammability <= 0)
                continue;

            var score = tileDef.Flammability;

            // no atmosphere = definitely dont score this tile (shouldnt be possible anyway afaik)
            if (_atmos.GetTileMixture((grid, gridComp, null), null, neighbor.Tile.GridIndices) is not { } mixture)
                continue;

            if (mixture.Temperature > Atmospherics.FireMinimumTemperatureToSpread)
                score *= 4;
            else if (mixture.Temperature > Atmospherics.FireMinimumTemperatureToExist)
                score *= 2;

            // TODO ES fires dont actually fizzle out if theres no oxygen in the tile -after- they spread
            // TODO ES and tile fires dont use oxygen either
            // TODO ES also it'd be nice if they did something similar to smoke, where it can also choose to
            // spread more fire into a tile that already has fire on it, instead of having to spread to
            // a new tile
            if (mixture.GetMoles(Gas.Oxygen) < ent.Comp.MinimumOxyMolesToSpread)
                score *= 0;
            if (_atmos.GetHeatCapacity(mixture, false) < Atmospherics.MinimumHeatCapacity)
                score *= 0;

            var coords = MapSys.GridTileToLocal(neighbor.Tile.GridUid, neighbor.Grid, neighbor.Tile.GridIndices);

            if (score > 0)
                weights.Add(coords, score);
        }

        while (args.Updates > 0)
        {
            if (flammable.FireStacks < ent.Comp.MinFirestacksToSpread || weights.Count == 0)
                return;

            var coords = _random.PickAndTake(weights);
            Spawn(ent.Comp.Prototype, coords);

            _flammable.AdjustFireStacks(ent, _random.NextFloat(0.25f, 1.25f) * -ent.Comp.FirestacksRemoveOnSpread, flammable);
            args.Updates--;
        }
    }
    #endregion

    #region API

    [PublicAPI]
    public override bool TryDoTileFire(EntityCoordinates coords, EntityUid? originatingUser = null, int stage = 1)
    {
        var xform = Transform(coords.EntityId);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid))
            return false;

        var tile = XformSys.GetGridTilePositionOrDefault((coords.EntityId, xform), mapGrid);

        if (_atmos.IsTileAirBlocked(grid, tile, mapGridComp: mapGrid))
            return false;

        // ESTileFire vs ESTileFireStage2/3/4
        EntProtoId proto = stage == 1 ? _stage1Fire : $"{_stage1Fire}Stage{stage}";

        SpawnAtPosition(proto, coords);

        var ev = new ESTileFireCreatedEvent(coords, originatingUser, stage);
        RaiseLocalEvent(ref ev);

        // TODO arsonist update counter objective for originating user u get the idea etc etc
        return true;
    }

    #endregion
}

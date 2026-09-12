using System.Numerics;
using Content.Shared._CE.Planets.Shields;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Server._CE.ZLevels.PVS;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Enums;

namespace Content.Server._CE.Planets.Shields;

public sealed partial class CEShieldBeamSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CESharedZLevelsSystem _levels = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private IMapManager _mapManager = default!;
    private float _elapsed;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void Start(Entity<CEShieldGeneratorComponent> generator)
    {
        Stop(generator);
        var xform = Transform(generator);
        if (xform.MapUid is not { } map)
            return;

        SyncControls(generator, map);
    }

    private void SyncControls(Entity<CEShieldGeneratorComponent> generator, EntityUid map)
    {
        var maps = new HashSet<EntityUid> { map };
        if (TryComp<CEZMapComponent>(map, out var level))
        {
            maps.UnionWith(_levels.GetAllMapsAbove((map, level)));
            var transits = EntityQueryEnumerator<CEZTransitMapComponent>();
            while (transits.MoveNext(out var transitMap, out var transit))
            {
                if (transit.LowerMap is { } lower && TryComp<CEZMapComponent>(lower, out var anchor)
                    && anchor.NetworkUid == level.NetworkUid && anchor.Depth >= level.Depth)
                    maps.Add(transitMap);
            }
        }

        for (var i = generator.Comp.BeamControls.Count - 1; i >= 0; i--)
        {
            var control = generator.Comp.BeamControls[i];
            if (!TerminatingOrDeleted(control) && Transform(control).MapUid is { } controlMap
                && maps.Remove(controlMap))
                continue;
            if (!TerminatingOrDeleted(control))
                QueueDel(control);
            generator.Comp.BeamControls.RemoveAt(i);
        }

        var xform = Transform(generator);
        var position = _transform.GetWorldPosition(xform);
        foreach (var target in maps)
        {
            var uid = Spawn("CEShieldBeam", new EntityCoordinates(target, position));
            EnsureComp<CEPvsOverrideComponent>(uid);
            var beam = Comp<CEShieldBeamComponent>(uid);
            beam.Generator = GetNetEntity(generator);
            beam.Source = target == map;
            beam.Overlay = generator.Comp.BeamOverlay;
            Dirty(uid, beam);
            generator.Comp.BeamControls.Add(uid);
        }
    }

    public void Stop(Entity<CEShieldGeneratorComponent> generator)
    {
        foreach (var uid in generator.Comp.BeamControls)
        {
            if (!TerminatingOrDeleted(uid))
                QueueDel(uid);
        }
        generator.Comp.BeamControls.Clear();
    }

    public override void Update(float frameTime)
    {
        var generators = EntityQueryEnumerator<CEShieldGeneratorComponent, TransformComponent>();
        while (generators.MoveNext(out var generatorUid, out var generatorComp, out var generatorTransform))
        {
            if (generatorComp.Stage != CEShieldGeneratorStage.Active || generatorTransform.MapUid is not { } map)
                continue;
            SyncControls((generatorUid, generatorComp), map);
        }

        _elapsed += frameTime;
        if (_elapsed < 0.1f)
            return;
        _elapsed = 0f;

        var query = EntityQueryEnumerator<CEShieldBeamComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beam, out var xform))
        {
            if (!TryGetEntity(beam.Generator, out var owner) || owner is not { } generator
                || !TryComp<CEShieldGeneratorComponent>(generator, out var gen)
                || gen.Stage != CEShieldGeneratorStage.Active || TerminatingOrDeleted(generator))
            {
                QueueDel(uid);
                continue;
            }

            var position = _transform.GetWorldPosition(generator);
            _transform.SetWorldPosition(uid, position);
            foreach (var victim in _lookup.GetEntitiesInRange(uid, beam.Radius, LookupFlags.Uncontained))
            {
                if (victim == generator || HasComp<CEShieldBeamComponent>(victim)
                    || HasComp<MapComponent>(victim) || HasComp<MapGridComponent>(victim)
                    || TerminatingOrDeleted(victim) || EntityManager.IsQueuedForDeletion(victim))
                    continue;

                Vaporize(victim, uid, beam.Generator);
            }

            if (beam.Source)
                continue;

            var bounds = Box2.CenteredAround(position, new Vector2(beam.Radius * 2));
            var grids = new List<Entity<MapGridComponent>>();
            _mapManager.FindGridsIntersecting(xform.MapID, bounds, ref grids);
            foreach (var grid in grids)
            {
                var tiles = new List<(Vector2i, Tile)>();
                foreach (var tile in _maps.GetTilesIntersecting(grid.Owner, grid.Comp, bounds))
                    tiles.Add((tile.GridIndices, Tile.Empty));
                _maps.SetTiles(grid.Owner, grid.Comp, tiles);
            }
        }
    }

    private void Vaporize(EntityUid victim, EntityUid beam, NetEntity generator)
    {
        if (TerminatingOrDeleted(victim) || EntityManager.IsQueuedForDeletion(victim))
            return;
        var ev = new CEShieldBeamVaporizingEvent(beam, generator);
        RaiseLocalEvent(victim, ref ev);
        if (ev.Handled)
            return;
        var children = new List<EntityUid>();
        var enumerator = Transform(victim).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
            children.Add(child);
        foreach (var child in children)
            Vaporize(child, beam, generator);
        QueueDel(victim);
    }
}

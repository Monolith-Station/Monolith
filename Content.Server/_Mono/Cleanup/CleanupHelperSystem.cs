using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Server._Mono.Cleanup;

/// <summary>
///     System with helper methods for entity cleanup.
/// </summary>
public sealed class CleanupHelperSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private List<Entity<MapGridComponent>> _gridsFound = new();

    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<MindComponent> _mindQuery;

    public override void Initialize()
    {
        base.Initialize();

        _ghostQuery = GetEntityQuery<GhostComponent>();
        _mindQuery = GetEntityQuery<MindComponent>();
    }

    /// <summary>
    ///     Whether there is an entity with a player bound to it in radius. Counts dead people and brains but not ghosts.
    /// </summary>
    public bool HasNearbyPlayers(EntityCoordinates coord, float radius)
    {
        var minds = _lookup.GetEntitiesInRange<MindContainerComponent>(coord, radius);

        foreach (var (uid, comp) in minds)
        {
            if (!comp.HasMind
                || _ghostQuery.HasComp(uid)
                || _mindQuery.CompOrNull(comp.Mind.Value)?.OwnedEntity == null
            )
                continue;

            var entCoord = Transform(uid).Coordinates;

            if (coord.TryDistance(EntityManager, entCoord, out var distance)
                && distance <= radius
            )
                return true;
        }
        return false;
    }

    /// <summary>
    ///     Whether there is a grid in radius. Approximate.
    /// </summary>
    public bool HasNearbyGrids(EntityCoordinates coord, float radius)
    {
        var rangeVec = new Vector2(radius, radius);
        var mapPos = _transform.ToMapCoordinates(coord);
        var pos = mapPos.Position;

        _gridsFound.Clear();
        _mapMan.FindGridsIntersecting(mapPos.MapId, new Box2(pos - rangeVec, pos + rangeVec), ref _gridsFound, true);

        return _gridsFound.Count > 0;
    }
}

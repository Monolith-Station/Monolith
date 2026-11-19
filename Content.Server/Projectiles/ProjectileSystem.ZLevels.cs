// Mono: Cross-Z projectile routing.
using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.Projectiles;

public sealed partial class ProjectileSystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private IMapManager _zShotMaps = default!;
    [Dependency] private SharedMapSystem _zShotTiles = default!;

    internal readonly record struct ZShot(EntityUid Source, EntityUid? UpperMap, Vector2 Aim, Vector2 Direction);

    internal ZShot? PlanZShot(EntityUid? user, MapCoordinates from, MapCoordinates aim)
    {
        if (user is not { } shooter || from.MapId != aim.MapId ||
            !TryComp(shooter, out TransformComponent? xform) ||
            xform.MapID != from.MapId || xform.MapUid is not { } map)
            return null;

        var delta = aim.Position - from.Position;
        var length = delta.Length();
        if (!float.IsFinite(length) || length <= 0f)
            return null;

        if (TryComp<CEZLevelViewerComponent>(shooter, out var viewer) && viewer.LookUp)
        {
            return _zLevels.TryMapUp(map, out var above) && !HasZShotFloor(above, aim.Position)
                ? new ZShot(map, above, aim.Position, delta / length)
                : null;
        }

        if ((!_zLevels.TryMapDown(map, out _) &&
            (!TryComp<CEZTransitMapComponent>(map, out var transit) || transit.TransitBelow == null)) ||
            HasZShotFloor(map, aim.Position))
            return null;

        return new ZShot(map, null, aim.Position, delta / length);
    }

    internal void ArmZShot(EntityUid uid, ZShot shot)
    {
        EnsureComp<CEZShotComponent>(uid).Shot = shot;
    }

    private bool TryGetZShotCrossing(Entity<CEZShotComponent> entity, TransformComponent xform, Vector2 position, Vector2 step,
        out float fraction, out MapCoordinates destination)
    {
        fraction = 0f;
        destination = default;
        var shot = entity.Comp.Shot;
        var advance = Vector2.Dot(step, shot.Direction);
        if (xform.MapUid != shot.Source || advance <= 0f)
        {
            RemComp<CEZShotComponent>(entity);
            return false;
        }

        var toOpening = Vector2.Dot(shot.Aim - position, shot.Direction);
        if (toOpening > advance)
            return false;

        fraction = Math.Clamp(toOpening / advance, 0f, 1f);
        var crossing = position + step * fraction;
        var map = MapId.Nullspace;
        var canCross = shot.UpperMap is { } upper
            ? TryGetUpwardShotDestination(shot.Source, upper, crossing, out map)
            : !HasZShotFloor(shot.Source, crossing) && TryGetZShotDestination(shot.Source, crossing, out map);
        if (!canCross)
        {
            RemComp<CEZShotComponent>(entity);
            return false;
        }

        destination = new MapCoordinates(crossing, map);
        return true;
    }

    private bool TryGetUpwardShotDestination(EntityUid source, EntityUid upper, Vector2 crossing, out MapId destination)
    {
        destination = MapId.Nullspace;
        if (!_zLevels.TryMapUp(source, out var above) || above.Owner != upper ||
            TerminatingOrDeleted(upper) || !TryComp<MapComponent>(upper, out var map) || HasZShotFloor(upper, crossing))
            return false;

        EntityUid? lower = source;
        var progress = 0f;
        if (TryComp<CEZTransitMapComponent>(source, out var sourceTransit))
        {
            lower = sourceTransit.LowerMap;
            progress = _zLevels.GetTransitProgress(sourceTransit);
        }

        var query = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (query.MoveNext(out var uid, out var transit))
        {
            if (uid != source && lower != null && transit.LowerMap == lower && transit.UpperMap == upper &&
                !TerminatingOrDeleted(uid) && _zLevels.GetTransitProgress(transit) > progress &&
                HasZShotFloor(uid, crossing))
                return false;
        }

        destination = map.MapId;
        return true;
    }

    private bool TryGetZShotDestination(EntityUid source, Vector2 position, out MapId destination)
    {
        destination = MapId.Nullspace;
        EntityUid? lower = null;
        if (_zLevels.TryMapDown(source, out var staticLower) && TryComp<MapComponent>(staticLower, out var lowerMap))
        {
            lower = staticLower.Owner;
            destination = lowerMap.MapId;
        }

        var upper = source;
        var maxProgress = 1f;
        if (TryComp<CEZTransitMapComponent>(source, out var sourceTransit))
        {
            if (sourceTransit.UpperMap is { } upperAnchor)
                upper = upperAnchor;
            maxProgress = _zLevels.GetTransitProgress(sourceTransit);

            // The linked deck is below the fixed lower anchor. Only reach it through a gap in that anchor.
            if (sourceTransit.TransitBelow is { } linked && !TerminatingOrDeleted(linked) &&
                (lower == null || !HasZShotFloor(lower.Value, position)) &&
                TryComp<MapComponent>(linked, out var linkedMap) && HasZShotFloor(linked, position))
                destination = linkedMap.MapId;
        }

        var bestProgress = -1f;
        var query = EntityQueryEnumerator<CEZTransitMapComponent, MapComponent>();
        while (query.MoveNext(out var uid, out var transit, out var map))
        {
            if (uid == source || lower == null || transit.LowerMap != lower || transit.UpperMap != upper ||
                TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
                continue;

            var progress = _zLevels.GetTransitProgress(transit);
            if ((sourceTransit != null ? progress >= maxProgress : progress > maxProgress) ||
                progress <= bestProgress || !HasZShotFloor(uid, position))
                continue;

            bestProgress = progress;
            destination = map.MapId;
        }

        return destination != MapId.Nullspace;
    }

    private bool HasZShotFloor(EntityUid map, Vector2 position)
    {
        return _zShotMaps.TryFindGridAt(map, position, out var uid, out var grid) &&
               _zShotTiles.TryGetTileRef(uid, grid, position, out var tile) && !tile.Tile.IsEmpty;
    }

    private void SetZShotMapVelocity(EntityUid uid, TransformComponent xform, PhysicsComponent body, Vector2 velocity)
    {
        var parentVelocity = _physics.GetMapLinearVelocity(uid, body, xform) - body.LinearVelocity;
        _physics.SetLinearVelocity(uid, velocity - parentVelocity, body: body);
    }
}

[RegisterComponent]
public sealed partial class CEZShotComponent : Component
{
    internal ProjectileSystem.ZShot Shot;
    internal bool Crossed;
    internal Vector2? RestoreVelocity;
}

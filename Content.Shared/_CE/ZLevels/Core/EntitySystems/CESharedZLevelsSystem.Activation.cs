/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Projectiles;
using JetBrains.Annotations;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    private readonly List<EntityUid> _activeBodies = new();

    public IReadOnlyList<EntityUid> ActiveBodies => _activeBodies;

    private void InitializeActivation()
    {
        SubscribeLocalEvent<CEZPhysicsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEZPhysicsComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<CEZPhysicsComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<CEZPhysicsComponent, PhysicsBodyTypeChangedEvent>(OnPhysicsBodyTypeChanged);

        SubscribeLocalEvent<CEZPhysicsComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<CEZPhysicsComponent, ProjectileShotEvent>(OnProjectileShot);
        SubscribeLocalEvent<CEZPhysicsComponent, EmbedEvent>(OnProjectileEmbed);
    }

    private void OnMapInit(Entity<CEZPhysicsComponent> entity, ref MapInitEvent args)
    {
        RefreshBody(entity);

        var mapUid = Transform(entity).MapUid;

        if (!_zMapQuery.TryComp(mapUid, out var zLevel))
            return;

        if (entity.Comp.CurrentZLevel == zLevel.Depth)
            return;

        entity.Comp.CurrentZLevel = zLevel.Depth;
        DirtyField(entity, entity.Comp, nameof(CEZPhysicsComponent.CurrentZLevel));
    }

    private void OnShutdown(Entity<CEZPhysicsComponent> entity, ref ComponentShutdown args)
    {
        SleepBody((entity, entity));
    }

    private void OnAnchorStateChanged(Entity<CEZPhysicsComponent> entity, ref AnchorStateChangedEvent args)
    {
        RefreshBody(entity);
    }

    private void OnPhysicsBodyTypeChanged(Entity<CEZPhysicsComponent> entity, ref PhysicsBodyTypeChangedEvent args)
    {
        RefreshBody(entity);
    }

    private void OnParentChanged(Entity<CEZPhysicsComponent> entity, ref EntParentChangedMessage args)
    {
        RefreshBody(entity);
        if (!IsDirectlyOnGridOrMap(Transform(entity)))
        {
            SetProjectileFlight(entity, false);
            ResetZState(entity);
            return;
        }

        SnapOutOfTransitMap(entity);

        // Drop leftover altitude that can no longer resolve to the ground here, before the
        // eye offset, sprite lift, and depth blur latch onto a height this map can't shed:
        //  - the entity left the z-network for an ordinary map (a warp, or the z/transit map
        //    was deleted under it); or
        //  - it is weightless (velocityGravity off — only ghosts/observers carry that), so
        //    no gravity will ever bring it back down. A ghost leaving a transit map hits
        //    this: SnapOutOfTransitMap hands it the gap's height on the way out and, with no
        //    fall to spend it, it would otherwise read as a permanent level up, fog and all.
        // SnapOutOfTransitMap already re-homes anything that landed ON a transit map, so a
        // non-z, non-transit map here is a genuine exit.
        var landedMap = Transform(entity).MapUid;
        var leftZNetwork = !_zMapQuery.HasComp(landedMap) && !HasComp<CEZTransitMapComponent>(landedMap);

        if (entity.Comp.LocalPosition != 0f && (leftZNetwork || !entity.Comp.VelocityGravity))
            ResetZState(entity);
    }

    private void ResetZState(Entity<CEZPhysicsComponent> entity)
    {
        if (entity.Comp.LocalPosition != 0f)
        {
            entity.Comp.LocalPosition = 0f;
            DirtyField(entity, entity.Comp, nameof(CEZPhysicsComponent.LocalPosition));
        }

        if (entity.Comp.Velocity != 0f)
        {
            entity.Comp.Velocity = 0f;
            DirtyField(entity, entity.Comp, nameof(CEZPhysicsComponent.Velocity));
        }
    }

    private static bool IsDirectlyOnGridOrMap(TransformComponent xform) =>
        xform.ParentUid == xform.GridUid || xform.ParentUid == xform.MapUid;

    private bool CanBeActive(EntityUid uid, TransformComponent xform) =>
        IsDirectlyOnGridOrMap(xform) && !xform.Anchored &&
        (!_physicsQuery.TryComp(uid, out var physics) || physics.BodyType != BodyType.Static);

    /// <summary>
    /// If an entity falls off of a transit map, put it back onto a normal one.
    /// </summary>
    private void SnapOutOfTransitMap(Entity<CEZPhysicsComponent> entity)
    {
        // Grids don't count. We don't talk about grids.
        if (_gridQuery.HasComp(entity) || _mapQuery.HasComp(entity))
            return;

        var xform = Transform(entity);

        // Not on the map itself. Don't touch it.
        if (xform.ParentUid != xform.MapUid)
            return;

        if (!TryComp<CEZTransitMapComponent>(xform.MapUid, out var transit))
            return;

        if (!entity.Comp.ProjectileFlight && ZPhysicsQuery.TryComp(transit.PrimaryGrid, out var primaryPhysics))
            SetZPosition((entity, entity), primaryPhysics.LocalPosition);

        if (TryMove(entity, -1))
        {
            if (entity.Comp.ProjectileFlight)
                ResetZState(entity);
            else
                WakeBody((entity, entity));
        }
    }

    private void OnProjectileShot(Entity<CEZPhysicsComponent> entity, ref ProjectileShotEvent args)
    {
        // A shot spawned or offset past the hull may already have dropped out of transit.
        if (HasComp<CEZTransitMapComponent>(args.LaunchMap) && Transform(entity).MapUid != args.LaunchMap)
            ResetZState(entity);

        SetProjectileFlight(entity, true);
    }

    private void OnProjectileEmbed(Entity<CEZPhysicsComponent> entity, ref EmbedEvent args) =>
        SetProjectileFlight(entity, false);

    private void SetProjectileFlight(Entity<CEZPhysicsComponent> entity, bool flying)
    {
        if (entity.Comp.ProjectileFlight == flying)
            return;

        entity.Comp.ProjectileFlight = flying;
        DirtyField(entity, entity.Comp, nameof(CEZPhysicsComponent.ProjectileFlight));
    }

    [PublicAPI]
    public void WakeBody(Entity<CEZPhysicsComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        if (_activeBodies.Contains(entity) ||
            !_transformQuery.TryComp(entity, out var xform) || !CanBeActive(entity, xform))
            return;

        entity.Comp.Sleeping = false;
        entity.Comp.SleepTimer = 0f;

        _activeBodies.Add(entity);
    }

    [PublicAPI]
    public void SleepBody(Entity<CEZPhysicsComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        entity.Comp.Sleeping = true;
        entity.Comp.SleepTimer = 0f;

        _activeBodies.Remove(entity);
    }

    [PublicAPI]
    public void RefreshBody(Entity<CEZPhysicsComponent> entity)
    {
        if (TerminatingOrDeleted(entity))
        {
            SleepBody((entity, entity));
            return;
        }

        if (!CanBeActive(entity, Transform(entity)))
        {
            SleepBody((entity, entity));
            return;
        }

        WakeBody((entity, entity));
    }
}

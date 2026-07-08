/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
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

        // Height inheritance only applies to lateral parent swaps (stepping on/off a
        // grid within the same z-level). Cross-map parent changes are z-hops: TryMove
        // reparents onto the destination grid mid-hop and ProcessZPhysics still owes
        // the ±1 carry, so clobbering LocalPosition here yo-yos the entity between
        // levels and leaves it "landed" one layer above the grid.
        if (args.OldMapId == Transform(entity).MapUid)
        {
            if (ZPhysicsQuery.TryComp(args.OldParent, out var oldParentPhysics))
                SetZPosition((entity, entity), oldParentPhysics.LocalPosition);
            else if (ZPhysicsQuery.HasComp(Transform(entity).ParentUid))
                SetZPosition((entity, entity), 0);
        }

        SnapOutOfTransitMap(entity);
    }

    /// <summary>
    /// A transit map is open air between two z-levels, so an entity that ends up
    /// parented to the map itself (stepped off a grid mid-gap, thrown overboard,
    /// dropped) has nothing to exist on there. Snap it to the lower z-level at the
    /// transit map's current height so it falls through real space instead of
    /// dangling on the gap map.
    /// </summary>
    private void SnapOutOfTransitMap(Entity<CEZPhysicsComponent> entity)
    {
        // Grids in transit are the transit machinery itself; they leave via
        // SetTransitAltitude / TryExitTransit, not this path.
        if (_gridQuery.HasComp(entity) || _mapQuery.HasComp(entity))
            return;

        var xform = Transform(entity);

        // Still standing on a grid within the gap: the grid carries it.
        if (xform.ParentUid != xform.MapUid)
            return;

        if (!TryComp<CEZTransitMapComponent>(xform.MapUid, out var transit))
            return;

        // The gap's current altitude above its lower anchor is the primary grid's
        // transit fraction.
        if (ZPhysicsQuery.TryComp(transit.PrimaryGrid, out var primaryPhysics))
            SetZPosition((entity, entity), primaryPhysics.LocalPosition);

        // MapOffset resolves -1 from a transit map to its lower anchor; LocalPosition
        // is preserved across the hop, so the entity keeps the transit height.
        if (TryMove(entity, -1))
            WakeBody((entity, entity));
    }

    [PublicAPI]
    public void WakeBody(Entity<CEZPhysicsComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        if (_activeBodies.Contains(entity))
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

        var transform = Transform(entity);
        var parent = transform.ParentUid;

        var onMap = parent == transform.GridUid || parent == transform.MapUid;

        if (!onMap
            || transform.Anchored
            || _physicsQuery.TryComp(entity, out var physics)
            && physics.BodyType == BodyType.Static)
        {
            SleepBody((entity, entity));
            return;
        }

        WakeBody((entity, entity));
    }
}

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
        SnapOutOfTransitMap(entity);
    }

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

        if (ZPhysicsQuery.TryComp(transit.PrimaryGrid, out var primaryPhysics))
            SetZPosition((entity, entity), primaryPhysics.LocalPosition);

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

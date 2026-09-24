/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedEyeSystem _eyeSystem = default!;

    private readonly EntProtoId _zEyeProto = "CEZLevelEye";

    private readonly TimeSpan _zLevelViewerUpdateRate = TimeSpan.FromSeconds(1f);
    private TimeSpan _nextZLevelViewerUpdate = TimeSpan.Zero;

    private readonly HashSet<EntityUid> _dirtyViewers = new();

    private void InitView()
    {
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<CEZLevelViewerComponent, MapInitEvent>(OnViewerInit);
        SubscribeLocalEvent<CEZLevelViewerComponent, ComponentRemove>(OnCompRemove);

        SubscribeLocalEvent<CEZLevelViewerComponent, MapUidChangedEvent>(OnViewerMapUidChanged);
        SubscribeLocalEvent<CEZPhysicsComponent, CEZLevelFallMapEvent>(OnZLevelFall);
    }

    private void UpdateView(float frameTime)
    {
        UpdateDirtyViewers();

        if (_timing.CurTime < _nextZLevelViewerUpdate)
            return;
        _nextZLevelViewerUpdate = _timing.CurTime + _zLevelViewerUpdateRate;

        var query = EntityQueryEnumerator<CEZLevelViewerComponent, EyeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var viewer, out var srcEye, out var xform))
        {
            foreach (var eye in viewer.Eyes)
            {
                // Eyes die with their map.
                if (TerminatingOrDeleted(eye))
                    continue;

                _transform.SetWorldPosition(eye, _transform.GetWorldPosition(xform));

                // Zoom propagation.
                var baseScale = srcEye.PvsScale * GetViewerZoom(uid, srcEye);
                var eyeMap = Transform(eye).MapUid;
                _eyeSystem.SetPvsScale(eye,
                    xform.MapUid is { } viewerMap && eyeMap is { } eyeMapUid
                        ? GetZEyePvsScale(viewerMap, eyeMapUid, baseScale)
                        : baseScale);
            }
        }
    }

    private float GetViewerZoom(EntityUid uid, EyeComponent srcEye)
    {
        var zoom = TryComp<ContentEyeComponent>(uid, out var contentEye)
            ? contentEye.TargetZoom
            : srcEye.Zoom;
        return Math.Max(zoom.X, zoom.Y);
    }

    private void QueueAllViewerUpdates()
    {
        var query = AllEntityQuery<CEZLevelViewerComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _dirtyViewers.Add(uid);
        }
    }

    private void UpdateDirtyViewers()
    {
        if (_dirtyViewers.Count == 0)
            return;

        foreach (var uid in _dirtyViewers)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<CEZLevelViewerComponent>(uid, out var viewer))
                continue;

            UpdateViewer((uid, viewer));
        }

        _dirtyViewers.Clear();
    }

    private void OnViewerInit(Entity<CEZLevelViewerComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.ActionId);
        _meta.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnCompRemove(Entity<CEZLevelViewerComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
        _meta.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);
        _dirtyViewers.Remove(ent.Owner);

        foreach (var eye in ent.Comp.Eyes)
        {
            QueueDel(eye);
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        var viewer = EnsureComp<CEZLevelViewerComponent>(ev.Entity);
        UpdateViewer((ev.Entity, viewer));
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        RemComp<CEZLevelViewerComponent>(ev.Entity);
    }

    private void OnViewerMapUidChanged(Entity<CEZLevelViewerComponent> ent, ref MapUidChangedEvent args)
    {
        // "Why don't you just move the viewer here" Because doing that causes a really stupid crash (I miss you already, Fable) that deferring it mitigates.
        _dirtyViewers.Add(ent.Owner);
    }

    private void UpdateViewer(Entity<CEZLevelViewerComponent> ent)
    {
        var eyes = ent.Comp.Eyes;
        foreach (var eye in ent.Comp.Eyes)
        {
            QueueDel(eye);
        }
        eyes.Clear();

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var xform = Transform(ent);
        var map = xform.MapUid;

        if (map is null)
            return;

        var globalPos = _transform.GetWorldPosition(xform);
        var pvsScale = TryComp<EyeComponent>(ent, out var srcEye)
            ? srcEye.PvsScale * GetViewerZoom(ent, srcEye)
            : 1f;
        var coveredMaps = new HashSet<EntityUid> { map.Value };

        for (var i = 1; i <= MaxZLevelsBelowRendering; i++)
        {
            if (!TryMapOffset(map.Value, -i, out var mapUidBelow))
                break;

            SpawnViewerEye(eyes, actor, map.Value, mapUidBelow, globalPos, pvsScale);
            coveredMaps.Add(mapUidBelow);
        }

        // We constantly load the upper z-level for the client so that you can quickly look up and climb stairs without PVS lag.
        if (TryMapUp(map.Value, out var aboveMapUid))
        {
            SpawnViewerEye(eyes, actor, map.Value, aboveMapUid, globalPos, pvsScale);
            coveredMaps.Add(aboveMapUid);
        }

        // Also handle transit maps that are within viewrange.
        var transitQuery = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (transitQuery.MoveNext(out var transitUid, out var transitComp))
        {
            if (transitUid == map.Value)
                continue;

            if (TerminatingOrDeleted(transitUid) || EntityManager.IsQueuedForDeletion(transitUid))
                continue;

            if (transitComp.LowerMap is { } transitLower && coveredMaps.Contains(transitLower) ||
                transitComp.UpperMap is { } transitUpper && coveredMaps.Contains(transitUpper))
            {
                SpawnViewerEye(eyes, actor, map.Value, transitUid, globalPos, pvsScale);
            }
        }
    }

    private void SpawnViewerEye(HashSet<EntityUid> eyes, ActorComponent actor, EntityUid viewerMap, EntityUid targetMap, Vector2 globalPos, float pvsScale)
    {
        var newEye = SpawnAtPosition(_zEyeProto, new EntityCoordinates(targetMap, globalPos));

        Transform(newEye).GridTraversal = false;
        _eyeSystem.SetPvsScale(newEye, GetZEyePvsScale(viewerMap, targetMap, pvsScale));
        _viewSubscriber.AddViewSubscriber(newEye, actor.PlayerSession);
        eyes.Add(newEye);
    }

    private float GetZEyePvsScale(EntityUid viewerMap, EntityUid eyeMap, float baseScale)
    {
        if (!TryComp<CEZMapComponent>(eyeMap, out var eyeZ))
            return baseScale;

        int viewerDepth;
        if (TryComp<CEZMapComponent>(viewerMap, out var viewerZ))
            viewerDepth = viewerZ.Depth;
        else if (TryComp<CEZTransitMapComponent>(viewerMap, out var transit) &&
                 TryComp<CEZMapComponent>(transit.LowerMap, out var lowerZ))
            viewerDepth = lowerZ.Depth + 1;
        else
            return baseScale;

        var levelsBelow = viewerDepth - eyeZ.Depth;
        if (levelsBelow <= 0)
            return baseScale;

        return baseScale * MathF.Pow(1f / CESharedZLevelsSystem.ZLevelViewShrink, levelsBelow);
    }

    private void OnZLevelFall(Entity<CEZPhysicsComponent> ent, ref CEZLevelFallMapEvent args)
    {
        // A dirty trick: we call PredictedPopup on the falling entity on SERVER.
        // This means that the one who is falling does not see the popup itself, but everyone around them does. This is what we need.
        _popup.PopupPredictedCoordinates(Loc.GetString("ce-zlevel-falling-popup", ("name", Identity.Name(ent, EntityManager))), Transform(ent).Coordinates, ent);
    }
}

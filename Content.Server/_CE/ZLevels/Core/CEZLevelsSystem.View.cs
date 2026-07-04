/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.IdentityManagement;
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

    private readonly HashSet<EntityUid> _queuedViewerUpdates = new();

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
        if (_queuedViewerUpdates.Count > 0)
        {
            foreach (var uid in _queuedViewerUpdates)
            {
                if (TerminatingOrDeleted(uid))
                    continue;

                if (TryComp<CEZLevelViewerComponent>(uid, out var viewer))
                    UpdateViewer((uid, viewer));
            }

            _queuedViewerUpdates.Clear();
        }

        if (_timing.CurTime < _nextZLevelViewerUpdate)
            return;
        _nextZLevelViewerUpdate = _timing.CurTime + _zLevelViewerUpdateRate;

        var query = EntityQueryEnumerator<CEZLevelViewerComponent, EyeComponent>();
        while (query.MoveNext(out var uid, out var viewer, out var srcEye))
        {
            foreach (var eye in viewer.Eyes)
            {
                // Eyes die with their map.
                if (TerminatingOrDeleted(eye))
                    continue;

                var xform = Transform(uid);

                _transform.SetWorldPosition(eye, _transform.GetWorldPosition(xform));

                var eyeMap = Transform(eye).MapUid;
                _eyeSystem.SetPvsScale(eye,
                    xform.MapUid is { } viewerMap && eyeMap is { } eyeMapUid
                        ? GetZEyePvsScale(viewerMap, eyeMapUid, srcEye.PvsScale)
                        : srcEye.PvsScale);
            }
        }
    }

    private void OnViewerInit(Entity<CEZLevelViewerComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ZLevelActionEntity, ent.Comp.ActionProto);
        _meta.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnCompRemove(Entity<CEZLevelViewerComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelActionEntity);
        _meta.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);

        foreach (var eye in ent.Comp.Eyes)
        {
            if (!TerminatingOrDeleted(eye))
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
        // MapUidChangedEvent is raised while the transform system is still recursively
        // walking the child tree of whatever moved (e.g. a grid changing z-levels with
        // this viewer aboard). Rebuilding eyes spawns entities, and a spawn can attach
        // itself to that same mid-move tree and corrupt the enumeration, so defer it.
        _queuedViewerUpdates.Add(ent);
    }

    private void UpdateViewer(Entity<CEZLevelViewerComponent> ent)
    {
        var eyes = ent.Comp.Eyes;
        foreach (var eye in ent.Comp.Eyes)
        {
            // Eyes on a deleted transit map died with it.
            if (!TerminatingOrDeleted(eye))
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
        var pvsScale = TryComp<EyeComponent>(ent, out var srcEye) ? srcEye.PvsScale : 1f;

        // Maps this viewer's eye chain covers; used to find adjacent transit maps.
        var coveredMaps = new HashSet<EntityUid> { map.Value };

        // Riding a grid between levels: the view chain hangs off the gap being crossed.
        EntityUid? mapAbove = null;
        var belowChainStart = map.Value;
        var includeChainStart = false;
        if (TryComp<CEZTransitMapComponent>(map, out var transit))
        {
            mapAbove = transit.UpperMap;
            if (transit.LowerMap is { } lower)
            {
                belowChainStart = lower;
                includeChainStart = true;
            }
        }
        else if (TryMapUp(map.Value, out var aboveMapUid))
        {
            mapAbove = aboveMapUid.Value;
        }

        if (includeChainStart)
        {
            SpawnViewerEye(eyes, actor, belowChainStart, globalPos, pvsScale, map);
            coveredMaps.Add(belowChainStart);
        }

        for (var i = 1; i <= MaxZLevelsBelowRendering; i++)
        {
            if (!TryMapOffset(belowChainStart, -i, out var mapUidBelow))
                break;

            SpawnViewerEye(eyes, actor, mapUidBelow.Value, globalPos, pvsScale, map);
            coveredMaps.Add(mapUidBelow.Value);
        }

        // We constantly load the upper z-level for the client so that you can quickly look up and climb stairs without PVS lag.
        if (mapAbove != null)
        {
            SpawnViewerEye(eyes, actor, mapAbove.Value, globalPos, pvsScale);
            coveredMaps.Add(mapAbove.Value);
        }

        // Grids transiting past any covered level must stream to this viewer too:
        // both PVS contents and decal chunks key off view subscriptions.
        var transitQuery = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (transitQuery.MoveNext(out var transitUid, out var transitComp))
        {
            if (transitUid == map.Value)
                continue;

            // A transit hop queues this rebuild AND deletes the old map in the same
            // tick; never spawn an eye on a map that's already on death row.
            if (TerminatingOrDeleted(transitUid) || EntityManager.IsQueuedForDeletion(transitUid))
                continue;

            if (transitComp.LowerMap is { } transitLower && coveredMaps.Contains(transitLower) ||
                transitComp.UpperMap is { } transitUpper && coveredMaps.Contains(transitUpper))
            {
                SpawnViewerEye(eyes, actor, transitUid, globalPos, pvsScale);
            }
        }
    }

    /// <summary>
    /// Transit maps appear and disappear without viewers changing maps, so eye sets
    /// have to be rebuilt when one is created or removed.
    /// </summary>
    internal void QueueAllViewerUpdates()
    {
        var query = EntityQueryEnumerator<CEZLevelViewerComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _queuedViewerUpdates.Add(uid);
        }
    }

    private void SpawnViewerEye(HashSet<EntityUid> eyes, ActorComponent actor, EntityUid mapUid, Vector2 globalPos, float pvsScale, EntityUid? viewerMap = null)
    {
        // SpawnAttachedTo: eyes must parent to the map itself, never to a grid that
        // happens to occupy the position (SpawnAtPosition does a grid lookup).
        var newEye = SpawnAttachedTo(_zEyeProto, new EntityCoordinates(mapUid, globalPos));

        Transform(newEye).GridTraversal = false;
        _eyeSystem.SetPvsScale(newEye, viewerMap is { } viewer ? GetZEyePvsScale(viewer, mapUid, pvsScale) : pvsScale);
        _viewSubscriber.AddViewSubscriber(newEye, actor.PlayerSession);
        eyes.Add(newEye);
    }

    /// <summary>
    /// Eyes on levels below the viewer need a wider PVS range: the client draws those
    /// levels zoomed out by <see cref="CESharedZLevelsSystem.ZLevelViewShrink"/> per
    /// level, so the visible world area grows by the inverse.
    /// </summary>
    private float GetZEyePvsScale(EntityUid viewerMap, EntityUid eyeMap, float baseScale)
    {
        if (!TryComp<CEZLevelMapComponent>(eyeMap, out var eyeZ))
            return baseScale;

        int viewerDepth;
        if (TryComp<CEZLevelMapComponent>(viewerMap, out var viewerZ))
            viewerDepth = viewerZ.Depth;
        else if (TryComp<CEZTransitMapComponent>(viewerMap, out var transit) &&
                 TryComp<CEZLevelMapComponent>(transit.LowerMap, out var lowerZ))
            viewerDepth = lowerZ.Depth + 1; // Top of the gap: covers the whole ride.
        else
            return baseScale;

        var levelsBelow = viewerDepth - eyeZ.Depth;
        if (levelsBelow <= 0)
            return baseScale;

        return baseScale * MathF.Pow(1f / CESharedZLevelsSystem.ZLevelViewShrink, levelsBelow);
    }

    private void OnZLevelFall(Entity<CEZPhysicsComponent> ent, ref CEZLevelFallMapEvent args)
    {
        //A dirty trick: we call PredictedPopup on the falling entity on SERVER.
        //This means that the one who is falling does not see the popup itself, but everyone around them does. This is what we need.
        _popup.PopupCoordinates(Loc.GetString("ce-zlevel-falling-popup", ("name", Identity.Name(ent, EntityManager))), Transform(ent).Coordinates, ent);
    }
}

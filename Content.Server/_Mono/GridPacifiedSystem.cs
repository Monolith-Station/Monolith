using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Mono;
using Content.Shared._Mono.Company;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Content.Server.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Content.Server._NF.CryoSleep;

namespace Content.Server._Mono;

/// <summary>
/// System that handles the GridPacifiedComponent, which has the GridPacifierComponent apply pacification to certain entities within range.
/// </summary>
public sealed class GridPacifiedSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTimeTracking = default!;

    private ISawmill _logger = default!;
    private static readonly TimeSpan RequiredPlaytime = TimeSpan.FromHours(1);
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridPacifiedComponent, ComponentStartup>(OnGridPacifiedStartup);
        SubscribeLocalEvent<GridPacifiedComponent, ComponentShutdown>(OnGridPacifiedShutdown);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        var uid = ev.Entity;
        var player = ev.Player;
        // Only affect players with less than 1 hour of overall playtime
        var getTime = _playTimeTracking.TryGetTrackerTimes(player, out var time);

        if (getTime == false)
        {
            _logger?.Info($"Could not find playtime for: {uid} id: {player}");
            return;
        }
        var overallPlaytime = _playTimeTracking.GetOverallPlaytime(player);
        if (overallPlaytime < RequiredPlaytime)
        {
            var comp = AddComp<GridPacifiedComponent>(uid);
            var curTime = _gameTiming.CurTime;
            comp.PacifiedTime = curTime + RequiredPlaytime - overallPlaytime;
            return;
        }
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        var uid = ev.Entity;
        RemComp<GridPacifiedComponent>(uid);
        return;
    }

    private void OnGridPacifiedStartup(EntityUid uid, GridPacifiedComponent component, ComponentStartup args)
    {

        if (HasComp<PacifiedComponent>(uid))
        {
            component.PrePacified = true;
            return;
        }

    }

    private void OnGridPacifiedShutdown(EntityUid uid, GridPacifiedComponent component, ComponentShutdown args)
    {
        RemovePacified(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;

        // Find all entities with a GridPacifiedComponent
        var query = EntityQueryEnumerator<GridPacifiedComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var _, out var xform))
        {
            // Check if it's time for the periodic update
            if (curTime < component.NextUpdate)
                continue;

            if (curTime > component.PacifiedTime)
            {
                RemComp<GridPacifiedComponent>(uid);
                continue;
            }

            // Schedule the next update
            component.NextUpdate = curTime + component.UpdateInterval;
            ProcessPacificationRange(uid, component, xform);
        }
    }

    /// <summary>
    /// Processes entities
    /// </summary>
    private void ProcessPacificationRange(EntityUid uid, GridPacifiedComponent component, TransformComponent xform)
    {
        var uidPos = _transform.GetMapCoordinates(uid, xform);
        var query = EntityQueryEnumerator<GridPacifierComponent, TransformComponent>();
        while (query.MoveNext(out var gridUid, out var gridComponent, out var gridXform))
        {
            // Skip if the grid is on a different map
            if (gridXform.MapUid != xform.MapUid)
                continue;

            var gridPos = _transform.GetMapCoordinates(gridUid, gridXform);
            var distance = (gridPos.Position - uidPos.Position).Length();
            if (component.PacifyRadius > distance)
            {
                ApplyPacified(uid, component);
                return;
            }
        }
        RemovePacified(uid, component);
    }

    /// <summary>
    /// Performs the actual pacification checks and applies Pacified if appropriate
    /// </summary>
    private void ApplyPacified(EntityUid entityUid, GridPacifiedComponent component)
    {
        // Skip entities that already have the Pacified component
        if (HasComp<PacifiedComponent>(entityUid))
            return;

        // All checks passed - apply pacification
        EnsureComp<PacifiedComponent>(entityUid);
    }

    /// <summary>
    /// Removes Pacified from an entity
    /// </summary>
    private void RemovePacified(EntityUid entityUid, GridPacifiedComponent component)
    {
        if (component.PrePacified == true)
            return;

        if (HasComp<PacifiedComponent>(entityUid))
        {
            RemComp<PacifiedComponent>(entityUid);
        }
    }
}

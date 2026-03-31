using Content.Server.GameTicking;
using Content.Server._NF.Shipyard.Systems;
using Content.Server.Station.Systems;
using Content.Shared._NF.CCVar;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.Components;
using Robust.Shared.Configuration;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._NF.Shipyard.Systems;

/// <summary>
/// Manages ship ownership and handles cleanup of ships when owners are offline too long
/// </summary>
public sealed class ShipOwnershipSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private bool _autoDeleteEnabled;
    private TimeSpan _ownerOfflineTimeout;
    private TimeSpan _inactiveTimeout;
    private TimeSpan _deletionGrace;
    private TimeSpan _checkInterval;
    private TimeSpan _nextCheckTime;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to player events to track when they join/leave
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        // Initialize tracking for ships
        SubscribeLocalEvent<ShipOwnershipComponent, ComponentStartup>(OnShipOwnershipStartup);

        Subs.CVar(_cfg, NFCCVars.ShipyardAutoDeleteEnabled, value => _autoDeleteEnabled = value, true);
        Subs.CVar(_cfg, NFCCVars.ShipyardAutoDeleteOwnerOfflineSeconds, value => _ownerOfflineTimeout = TimeSpan.FromSeconds(value), true);
        Subs.CVar(_cfg, NFCCVars.ShipyardAutoDeleteInactiveSeconds, value => _inactiveTimeout = TimeSpan.FromSeconds(value), true);
        Subs.CVar(_cfg, NFCCVars.ShipyardAutoDeleteGraceSeconds, value => _deletionGrace = TimeSpan.FromSeconds(value), true);
        Subs.CVar(_cfg, NFCCVars.ShipyardAutoDeleteCheckIntervalSeconds, value => _checkInterval = TimeSpan.FromSeconds(Math.Max(5f, value)), true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    /// <summary>
    /// Register a ship as being owned by a player
    /// </summary>
    public void RegisterShipOwnership(EntityUid gridUid, ICommonSession owningPlayer)
    {
        // Don't register ownership if the entity isn't valid
        if (!EntityManager.EntityExists(gridUid))
            return;

        // Add ownership component to the ship
        var comp = EnsureComp<ShipOwnershipComponent>(gridUid);
        comp.OwnerUserId = owningPlayer.UserId;
        comp.IsOwnerOnline = true;
        comp.LastStatusChangeTime = _gameTiming.CurTime;
        comp.LastPlayerActivityTime = _gameTiming.CurTime;
        comp.PendingDeletionTime = null;

        Dirty(gridUid, comp);

        // Log ship registration
        Logger.InfoS("shipOwnership", $"Registered ship {ToPrettyString(gridUid)} to player {owningPlayer.Name} ({owningPlayer.UserId})");
    }

    private void OnShipOwnershipStartup(EntityUid uid, ShipOwnershipComponent component, ComponentStartup args)
    {
        if (component.LastPlayerActivityTime == TimeSpan.Zero)
            component.LastPlayerActivityTime = _gameTiming.CurTime;

        // If player is already online, mark them as such
        if (_playerManager.TryGetSessionById(component.OwnerUserId, out var player))
        {
            component.IsOwnerOnline = true;
            component.LastStatusChangeTime = _gameTiming.CurTime;
        }

        Dirty(uid, component);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.Session == null)
            return;

        var userId = e.Session.UserId;
        var query = EntityQueryEnumerator<ShipOwnershipComponent>();

        // Update all ships owned by this player
        while (query.MoveNext(out var shipUid, out var ownership))
        {
            if (ownership.OwnerUserId != userId)
                continue;

            switch (e.NewStatus)
            {
                case SessionStatus.Connected:
                case SessionStatus.InGame:
                    // Player has connected, update ownership
                    ownership.IsOwnerOnline = true;
                    ownership.LastStatusChangeTime = _gameTiming.CurTime;
                    ownership.PendingDeletionTime = null;
                    Logger.DebugS("shipOwnership", $"Owner of ship {ToPrettyString(shipUid)} has connected");
                    break;

                case SessionStatus.Disconnected:
                    // Player has disconnected, update ownership
                    ownership.IsOwnerOnline = false;
                    ownership.LastStatusChangeTime = _gameTiming.CurTime;
                    Logger.DebugS("shipOwnership", $"Owner of ship {ToPrettyString(shipUid)} has disconnected");
                    break;
            }

            Dirty(shipUid, ownership);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_autoDeleteEnabled)
            return;

        var now = _gameTiming.CurTime;
        if (now < _nextCheckTime)
            return;

        _nextCheckTime = now + _checkInterval;

        var query = EntityQueryEnumerator<ShipOwnershipComponent>();
        while (query.MoveNext(out var shipUid, out var ownership))
        {
            if (TerminatingOrDeleted(shipUid))
                continue;

            var hasPlayersAboard = HasPlayersAboard(shipUid);
            if (hasPlayersAboard)
            {
                ownership.LastPlayerActivityTime = now;
                ownership.PendingDeletionTime = null;
                Dirty(shipUid, ownership);
                continue;
            }

            if (ownership.IsOwnerOnline)
            {
                ownership.PendingDeletionTime = null;
                Dirty(shipUid, ownership);
                continue;
            }

            var ownerOfflineFor = now - ownership.LastStatusChangeTime;
            var inactiveFor = now - ownership.LastPlayerActivityTime;
            var shouldArmDeletion = ownerOfflineFor >= _ownerOfflineTimeout && inactiveFor >= _inactiveTimeout;

            if (!shouldArmDeletion)
            {
                if (ownership.PendingDeletionTime != null)
                {
                    ownership.PendingDeletionTime = null;
                    Dirty(shipUid, ownership);
                }

                continue;
            }

            if (ownership.PendingDeletionTime == null)
            {
                ownership.PendingDeletionTime = now + _deletionGrace;
                Dirty(shipUid, ownership);
                Logger.InfoS("shipOwnership",
                    $"Armed auto-deletion for {ToPrettyString(shipUid)} in {_deletionGrace.TotalMinutes:F0} minutes (offline {ownerOfflineFor.TotalMinutes:F0} min, inactive {inactiveFor.TotalMinutes:F0} min).");
                continue;
            }

            if (now < ownership.PendingDeletionTime.Value)
                continue;

            // Final safeguard right before deletion.
            if (HasPlayersAboard(shipUid) || ownership.IsOwnerOnline)
            {
                ownership.LastPlayerActivityTime = now;
                ownership.PendingDeletionTime = null;
                Dirty(shipUid, ownership);
                continue;
            }

            DeleteOwnedShuttle(shipUid);
        }
    }

    private bool HasPlayersAboard(EntityUid shipUid)
    {
        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out _, out var actor, out var xform))
        {
            if (actor.PlayerSession.Status != SessionStatus.InGame)
                continue;

            if (xform.GridUid == shipUid)
                return true;
        }

        return false;
    }

    private void DeleteOwnedShuttle(EntityUid shuttleUid)
    {
        if (_station.GetOwningStation(shuttleUid) is { Valid: true } stationUid)
            _station.DeleteStation(stationUid);

        Logger.InfoS("shipOwnership", $"Auto-deleting inactive purchased shuttle {ToPrettyString(shuttleUid)}.");
        QueueDel(shuttleUid);
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server._NF.Shuttles.Components; // Frontier: FTL knockdown immunity
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Events;
using Content.Shared.Body.Components;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Timing;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using FTLMapComponent = Content.Shared.Shuttles.Components.FTLMapComponent;
using Content.Server.Salvage.Expeditions;
using Content.Shared._Mono.Ships;
using Content.Shared._Crescent.SpaceBiomes;
using Robust.Shared.Prototypes;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    /*
     * This is a way to move a shuttle from one location to another, via an intermediate map for fanciness.
     */

    private readonly SoundSpecifier _startupSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    private readonly SoundSpecifier _arrivalSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    private const float MassConstant = 50f; // Arbitrary, at this value massMultiplier = 0.65
    private const float MassMultiplierMin = 0.5f;
    private const float MassMultiplierMax = 5f;

    public float DefaultStartupTime;
    public float DefaultTravelTime;
    public float DefaultArrivalTime;
    //private float FTLCooldown;
    public float FTLMassLimit;
    private TimeSpan _hyperspaceKnockdownTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Left-side of the station we're allowed to use
    /// </summary>
    private float _index;

    /// <summary>
    /// Space between grids within hyperspace.
    /// </summary>
    private const float Buffer = 100f;

    /// <summary>
    /// How many times we try to proximity warp close to something before falling back to map-wideAABB.
    /// </summary>
    private const int FTLProximityIterations = 15; // Frontier: 5<15

    // Frontier: coordinate rollover
    /// <summary>
    /// Maximum X coordinate before rolling over.
    /// </summary>
    private const float MaxCoord = 20000f;

    /// <summary>
    /// Amount to subtract from X coordinate on rollover.
    /// </summary>
    private const float CoordRollover = 40000f;
    // End Frontier: coordinate rollover

    private readonly HashSet<EntityUid> _lookupEnts = new();
    private readonly HashSet<EntityUid> _immuneEnts = new();
    private readonly HashSet<Entity<NoFTLComponent>> _noFtls = new();

    private EntityQuery<BodyComponent> _bodyQuery;
    private EntityQuery<FTLSmashImmuneComponent> _immuneQuery;
    private EntityQuery<StatusEffectsComponent> _statusQuery;

    [Dependency] private readonly IEntityManager _entManager = default!; // Mono
    [Dependency] private readonly IPrototypeManager _protManager = default!; // Mono

    private void InitializeFTL()
    {
        SubscribeLocalEvent<StationPostInitEvent>(OnStationPostInit);
        SubscribeLocalEvent<FTLComponent, ComponentShutdown>(OnFtlShutdown);

        _bodyQuery = GetEntityQuery<BodyComponent>();
        _immuneQuery = GetEntityQuery<FTLSmashImmuneComponent>();
        _statusQuery = GetEntityQuery<StatusEffectsComponent>();

        _cfg.OnValueChanged(CCVars.FTLStartupTime, time => DefaultStartupTime = time, true);
        _cfg.OnValueChanged(CCVars.FTLTravelTime, time => DefaultTravelTime = time, true);
        _cfg.OnValueChanged(CCVars.FTLArrivalTime, time => DefaultArrivalTime = time, true);
        //_cfg.OnValueChanged(CCVars.FTLCooldown, time => FTLCooldown = time, true); Monolith FTL Drive sets cooldown
        _cfg.OnValueChanged(CCVars.FTLMassLimit, time => FTLMassLimit = time, true);
        _cfg.OnValueChanged(CCVars.HyperspaceKnockdownTime, time => _hyperspaceKnockdownTime = TimeSpan.FromSeconds(time), true);
    }

    private void OnFtlShutdown(Entity<FTLComponent> ent, ref ComponentShutdown args)
    {
        QueueDel(ent.Comp.VisualizerEntity);
        ent.Comp.VisualizerEntity = null;
    }

    private void OnStationPostInit(ref StationPostInitEvent ev)
    {
        // Add all grid maps as ftl destinations that anyone can FTL to.
        foreach (var gridUid in ev.Station.Comp.Grids)
        {
            var gridXform = _xformQuery.GetComponent(gridUid);

            if (gridXform.MapUid == null)
            {
                continue;
            }

            TryAddFTLDestination(gridXform.MapID, true, false, false, out _);
        }
    }

    /// <summary>
    /// Ensures the FTL map exists and returns it.
    /// </summary>
    private EntityUid EnsureFTLMap()
    {
        var query = AllEntityQuery<FTLMapComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            return uid;
        }

        var mapUid = _mapSystem.CreateMap(out var mapId);
        var ftlMap = AddComp<FTLMapComponent>(mapUid);

        _metadata.SetEntityName(mapUid, "FTL");
        Log.Debug($"Setup hyperspace map at {mapUid}");
        DebugTools.Assert(!_mapSystem.IsPaused(mapId));
        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = ftlMap.Parallax;

        return mapUid;
    }

    public StartEndTime GetStateTime(FTLComponent component)
    {
        var state = component.State;

        switch (state)
        {
            case FTLState.Starting:
            case FTLState.Travelling:
            case FTLState.Arriving:
            case FTLState.Cooldown:
                return component.StateTime;
            case FTLState.Available:
                return default;
            default:
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Updates the whitelist for this FTL destination.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="whitelist"></param>
    public void SetFTLWhitelist(Entity<FTLDestinationComponent?> entity, EntityWhitelist? whitelist)
    {
        if (!Resolve(entity, ref entity.Comp))
            return;

        if (entity.Comp.Whitelist == whitelist)
            return;

        entity.Comp.Whitelist = whitelist;
        _console.RefreshShuttleConsoles();
        Dirty(entity);
    }

    /// <summary>
    /// Adds the target map as available for FTL.
    /// </summary>
    public bool TryAddFTLDestination(MapId mapId, bool enabled, [NotNullWhen(true)] out FTLDestinationComponent? component)
    {
        return TryAddFTLDestination(mapId, enabled, true, false, out component);
    }

    public bool TryAddFTLDestination(MapId mapId, bool enabled, bool requireDisk, bool beaconsOnly, [NotNullWhen(true)] out FTLDestinationComponent? component)
    {
        var mapUid = _mapSystem.GetMapOrInvalid(mapId);
        component = null;

        if (!Exists(mapUid))
            return false;

        component = EnsureComp<FTLDestinationComponent>(mapUid);

        if (component.Enabled == enabled && component.RequireCoordinateDisk == requireDisk && component.BeaconsOnly == beaconsOnly)
            return true;

        component.Enabled = enabled;
        component.RequireCoordinateDisk = requireDisk;
        component.BeaconsOnly = beaconsOnly;

        _console.RefreshShuttleConsoles();
        Dirty(mapUid, component);
        return true;
    }

    [PublicAPI]
    public void RemoveFTLDestination(EntityUid uid)
    {
        if (!RemComp<FTLDestinationComponent>(uid))
            return;

        _console.RefreshShuttleConsoles();
    }

    /// <summary>
    /// Returns true if the grid can FTL. Used to block protected shuttles like the emergency shuttle.
    /// </summary>
    public bool CanFTL(EntityUid shuttleUid, [NotNullWhen(false)] out string? reason)
    {
        // Currently in FTL already
        if (HasComp<FTLComponent>(shuttleUid))
        {
            reason = Loc.GetString("shuttle-console-in-ftl");
            return false;
        }

        if (TryComp<PhysicsComponent>(shuttleUid, out var shuttlePhysics))
        {

            // Too large to FTL
            if (FTLMassLimit > 0 &&  shuttlePhysics.Mass > FTLMassLimit)
            {
                reason = Loc.GetString("shuttle-console-mass");
                return false;
            }
        }

        if (HasComp<PreventPilotComponent>(shuttleUid))
        {
            reason = Loc.GetString("shuttle-console-prevent");
            return false;
        }

        // Check if the shuttle is in an expedition
        if (TryComp<TransformComponent>(shuttleUid, out var xform) &&
            xform.MapUid != null &&
            HasComp<SalvageExpeditionComponent>(xform.MapUid))
        {
            reason = Loc.GetString("shuttle-console-in-expedition");
            return false;
        }

        var ev = new ConsoleFTLAttemptEvent(shuttleUid, false, string.Empty);
        RaiseLocalEvent(shuttleUid, ref ev, true);

        if (ev.Cancelled)
        {
            reason = ev.Reason;
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Moves a shuttle from its current position to the target one without any checks. Goes through the hyperspace map while the timer is running.
    /// </summary>
    public void FTLToCoordinates(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityCoordinates coordinates,
        Angle angle,
        float? startupTime = null,
        float? hyperspaceTime = null,
        string? priorityTag = null)
    {
        // Check if destination is an expedition map
        bool isExpedition = IsTargetExpedition(coordinates);

        // If going to an expedition, undock all other shuttles before FTL
        if (isExpedition)
        {
            // Get all docked shuttles, ignoring FTLLock status for expeditions
            var dockedShuttles = new HashSet<EntityUid>();
            GetAllDockedShuttlesIgnoringFTLLock(shuttleUid, dockedShuttles);

            Log.Info($"FTL to expedition detected. Shuttle {ToPrettyString(shuttleUid)} has {dockedShuttles.Count} docked shuttles (including self)");

            // Undock all other shuttles
            foreach (var dockedUid in dockedShuttles)
            {
                if (dockedUid == shuttleUid)
                    continue;

                Log.Info($"Undocking {ToPrettyString(dockedUid)} from {ToPrettyString(shuttleUid)} before expedition FTL");

                // Find docks connecting this shuttle to others
                var dockedShuttleDocks = _dockSystem.GetDocks(dockedUid);
                foreach (var dockPort in dockedShuttleDocks)
                {
                    if (!TryComp<DockingComponent>(dockPort, out var dockComp) || !dockComp.Docked || dockComp.DockedWith == null)
                        continue;

                    _dockSystem.Undock((dockPort, dockComp));
                }
            }
        }

        if (!TrySetupFTL(shuttleUid, component, out var hyperspace))
            return;

        startupTime ??= DefaultStartupTime;
        hyperspaceTime ??= DefaultTravelTime;

        hyperspace.StartupTime = startupTime.Value;
        hyperspace.TravelTime = hyperspaceTime.Value;
        hyperspace.StateTime = StartEndTime.FromStartDuration(
            _gameTiming.CurTime,
            TimeSpan.FromSeconds(hyperspace.StartupTime));
        hyperspace.TargetCoordinates = coordinates;
        hyperspace.TargetAngle = angle;
        hyperspace.PriorityTag = priorityTag;

        _console.RefreshShuttleConsoles(shuttleUid);

        var mapId = _transform.GetMapId(coordinates);
        var mapUid = _mapSystem.GetMap(mapId);
        var ev = new FTLRequestEvent(mapUid);
        RaiseLocalEvent(shuttleUid, ref ev, true);
    }

    /// <summary>
    /// Moves a shuttle from its current position to docked on the target one.
    /// If no docks are free when FTLing it will arrive in proximity
    /// </summary>
    public void FTLToDock(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityUid target,
        float? startupTime = null,
        float? hyperspaceTime = null,
        string? priorityTag = null)
    {
        // TODO: Validation
        if (!TryComp<FTLDestinationComponent>(_mapManager.GetMapEntityId(_transform.GetMapId(target)), out var dest))
        {
            return;
        }

        if (!dest.Enabled)
            return;

        // Check if destination is in an expedition map
        var targetCoords = new EntityCoordinates(target, Vector2.Zero);
        bool isExpedition = IsTargetExpedition(targetCoords);

        // If going to an expedition, undock all other shuttles before FTL
        if (isExpedition)
        {
            // Get all docked shuttles, ignoring FTLLock status for expeditions
            var dockedShuttles = new HashSet<EntityUid>();
            GetAllDockedShuttlesIgnoringFTLLock(shuttleUid, dockedShuttles);

            Log.Info($"FTL dock to expedition detected. Shuttle {ToPrettyString(shuttleUid)} has {dockedShuttles.Count} docked shuttles (including self)");

            // Undock all other shuttles - for expeditions, ALL docked shuttles must be undocked
            foreach (var dockedUid in dockedShuttles)
            {
                if (dockedUid == shuttleUid)
                    continue;

                Log.Info($"Undocking {ToPrettyString(dockedUid)} from {ToPrettyString(shuttleUid)} before expedition FTL");

                // Find docks connecting this shuttle to others
                var dockedShuttleDocks = _dockSystem.GetDocks(dockedUid);
                foreach (var dockPort in dockedShuttleDocks)
                {
                    if (!TryComp<DockingComponent>(dockPort, out var dockComp) || !dockComp.Docked || dockComp.DockedWith == null)
                        continue;

                    _dockSystem.Undock((dockPort, dockComp));
                }
            }
        }

        var hyperspace = EnsureComp<FTLComponent>(shuttleUid);
        SetupFTL(hyperspace, startupTime, hyperspaceTime, priorityTag);

        if (TryComp<DockingComponent>(target, out var dock) && dock.Docked && dock.DockedWith != null)
        {
            hyperspace.TargetCoordinates = new EntityCoordinates(dock.DockedWith.Value, Vector2.Zero);
            hyperspace.TargetAngle = _transform.GetWorldRotation(dock.DockedWith.Value) + Math.PI;
        }
        else if (TryFTLDock(shuttleUid, component, target, out var config))
        {
            hyperspace.TargetCoordinates = config.Coordinates;
            hyperspace.TargetAngle = config.Angle;
        }
        else if (TryGetFTLProximity(shuttleUid, new EntityCoordinates(target, Vector2.Zero), out var coords, out var targAngle))
        {
            hyperspace.TargetCoordinates = coords;
            hyperspace.TargetAngle = targAngle;
        }
        else
        {
            // FTL back to its own position.
            hyperspace.TargetCoordinates = Transform(shuttleUid).Coordinates;
            Log.Error($"Unable to FTL grid {ToPrettyString(shuttleUid)} to target properly?");
        }
    }

    /// <summary>
    /// Sets up the FTL component with startup and travel times and priority tag.
    /// </summary>
    private void SetupFTL(FTLComponent hyperspace, float? startupTime, float? hyperspaceTime, string? priorityTag)
    {
        startupTime ??= DefaultStartupTime;
        hyperspaceTime ??= DefaultTravelTime;

        hyperspace.StartupTime = startupTime.Value;
        hyperspace.TravelTime = hyperspaceTime.Value;
        hyperspace.StateTime = StartEndTime.FromStartDuration(
            _gameTiming.CurTime,
            TimeSpan.FromSeconds(hyperspace.StartupTime));
        hyperspace.PriorityTag = priorityTag;

        _console.RefreshShuttleConsoles(hyperspace.Owner);
    }

    /// <summary>
    /// Recursively gets all docked shuttles to the target shuttle.
    /// </summary>
    public void GetAllDockedShuttles(EntityUid shuttleUid, HashSet<EntityUid> dockedShuttles)
    {
        if (!dockedShuttles.Add(shuttleUid))
            return;  // Already processed this shuttle

        var docks = _dockSystem.GetDocks(shuttleUid);
        foreach (var dock in docks)
        {
            if (!TryComp<DockingComponent>(dock, out var dockComp) || dockComp.Docked == false)
                continue;
            if (dockComp.DockedWith == null)
                continue;
            var dockedGridUid = _transform.GetParentUid(dockComp.DockedWith.Value);
            if (dockedGridUid == EntityUid.Invalid || !HasComp<ShuttleComponent>(dockedGridUid))
                continue;

            // If the docked shuttle has no FTLLockComponent or has it but it's disabled, skip adding it
            // to the FTL travel group, but still check its connections for potential conflicts
            if (!TryComp<FTLLockComponent>(dockedGridUid, out var ftlLock) || !ftlLock.Enabled)
            {
                // Still check this shuttle's connections without adding it to dockedShuttles
                var nestedDocks = _dockSystem.GetDocks(dockedGridUid);
                foreach (var nestedDock in nestedDocks)
                {
                    if (!TryComp<DockingComponent>(nestedDock, out var nestedDockComp) ||
                        nestedDockComp.Docked == false ||
                        nestedDockComp.DockedWith == null)
                        continue;

                    var nestedDockedGridUid = _transform.GetParentUid(nestedDockComp.DockedWith.Value);
                    // Skip the original grid and any invalid grids
                    if (nestedDockedGridUid == EntityUid.Invalid ||
                        nestedDockedGridUid == shuttleUid ||
                        !HasComp<ShuttleComponent>(nestedDockedGridUid))
                        continue;

                    // Check if this grid should be added to the FTL travel group
                    if (TryComp<FTLLockComponent>(nestedDockedGridUid, out var nestedFtlLock) && nestedFtlLock.Enabled)
                    {
                        GetAllDockedShuttles(nestedDockedGridUid, dockedShuttles);
                    }
                }
                continue;
            }

            // If we haven't processed this grid yet, recursively get its docked shuttles
            if (!dockedShuttles.Contains(dockedGridUid))
            {
                GetAllDockedShuttles(dockedGridUid, dockedShuttles);
            }
        }
    }

    /// <summary>
    /// Recursively gets all docked shuttles to the target shuttle, ignoring FTLLock status.
    /// Used for expeditions where ALL docked shuttles must be undocked regardless of FTLLock.
    /// </summary>
    public void GetAllDockedShuttlesIgnoringFTLLock(EntityUid shuttleUid, HashSet<EntityUid> dockedShuttles)
    {
        if (!dockedShuttles.Add(shuttleUid))
            return;  // Already processed this shuttle

        var docks = _dockSystem.GetDocks(shuttleUid);
        foreach (var dock in docks)
        {
            if (!TryComp<DockingComponent>(dock, out var dockComp) || dockComp.Docked == false)
                continue;
            if (dockComp.DockedWith == null)
                continue;
            var dockedGridUid = _transform.GetParentUid(dockComp.DockedWith.Value);
            if (dockedGridUid == EntityUid.Invalid || !HasComp<ShuttleComponent>(dockedGridUid))
                continue;

            // For expeditions, we ignore FTLLock status and get ALL docked shuttles
            if (!dockedShuttles.Contains(dockedGridUid))
            {
                GetAllDockedShuttlesIgnoringFTLLock(dockedGridUid, dockedShuttles);
            }
        }
    }

    /// <summary>
    /// Sets up FTL for a shuttle after a console command.
    /// </summary>
    private bool TrySetupFTL(EntityUid uid, ShuttleComponent shuttle, [NotNullWhen(true)] out FTLComponent? component)
    {
        component = null;

        if (HasComp<FTLComponent>(uid))
        {
            Log.Warning($"Tried queuing {ToPrettyString(uid)} which already has {nameof(FTLComponent)}?");
            return false;
        }

        // Get all docked shuttles to determine which ones are traveling together
        var dockedShuttles = new HashSet<EntityUid>();
        GetAllDockedShuttles(uid, dockedShuttles);

        // Force undock emergency and arrivals shuttles
        if (HasComp<EmergencyShuttleComponent>(uid) || HasComp<ArrivalsShuttleComponent>(uid))
        {
            _dockSystem.UndockDocks(uid);
        }
        // For other shuttles, check if docked shuttles can FTL and only undock those that cannot
        else
        {
            // Check if all docked shuttles can FTL
            bool canAllFTL = true;
            foreach (var dockedUid in dockedShuttles)
            {
                if (dockedUid == uid)
                    continue;
                if (!CanFTL(dockedUid, out var reason))
                {
                    Log.Warning($"Cannot FTL due to docked shuttle {ToPrettyString(dockedUid)}: {reason}");
                    canAllFTL = false;
                    break;
                }
            }

            if (!canAllFTL)
                return false;

            // Instead of undocking all docks, we need to find docks that connect to non-shuttle entities
            // or entities not in our FTL group and undock only those
            var docks = _dockSystem.GetDocks(uid);
            foreach (var dock in docks)
            {
                if (!TryComp<DockingComponent>(dock, out var dockComp) || !dockComp.Docked || dockComp.DockedWith == null)
                    continue;

                var connectedEntityUid = _transform.GetParentUid(dockComp.DockedWith.Value);

                // If the connected entity is not in our FTL group or is not a shuttle, undock it
                if (connectedEntityUid == EntityUid.Invalid ||
                    !HasComp<ShuttleComponent>(connectedEntityUid) ||
                    !dockedShuttles.Contains(connectedEntityUid))
                {
                    _dockSystem.Undock((dock, dockComp));
                }
            }

            // Also check docks on other shuttles to handle the case where a shuttle with disabled FTLLock is in our dockedShuttles
            // but has docks to entities outside our FTL group
            foreach (var dockedShuttleUid in dockedShuttles)
            {
                if (dockedShuttleUid == uid)
                    continue;

                var dockedShuttleDocks = _dockSystem.GetDocks(dockedShuttleUid);
                foreach (var dock in dockedShuttleDocks)
                {
                    if (!TryComp<DockingComponent>(dock, out var dockComp) || !dockComp.Docked || dockComp.DockedWith == null)
                        continue;

                    var connectedEntityUid = _transform.GetParentUid(dockComp.DockedWith.Value);

                    // If the connected entity is not in our FTL group, undock it
                    if (connectedEntityUid == EntityUid.Invalid ||
                        !dockedShuttles.Contains(connectedEntityUid))
                    {
                        _dockSystem.Undock((dock, dockComp));
                    }
                }
            }
        }

        _thruster.DisableLinearThrusters(shuttle);
        _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.North);
        _thruster.SetAngularThrust(shuttle, false);

        component = AddComp<FTLComponent>(uid);
        component.State = FTLState.Starting;
        var audio = _audio.PlayPvs(_startupSound, uid);
        _audio.SetGridAudio(audio);
        component.StartupStream = audio?.Entity;

        // TODO: Play previs here for docking arrival.
        // Make sure the map is setup before we leave to avoid pop-in (e.g. parallax).
        EnsureFTLMap();
        return true;
    }

    /// <summary>
    /// Checks if the target coordinates are in an expedition map.
    /// </summary>
    private bool IsTargetExpedition(EntityCoordinates coordinates)
    {
        if (!Exists(coordinates.EntityId))
            return false;

        var mapId = _transform.GetMapId(coordinates);
        var mapUid = _mapSystem.GetMap(mapId);

        return HasComp<SalvageExpeditionComponent>(mapUid);
    }

    /// <summary>
    /// Shuttle travelling.
    /// </summary>
    private void UpdateFTLTravelling(Entity<FTLComponent, ShuttleComponent> entity)
    {
        var uid = entity.Owner;
        var comp = entity.Comp1;
        // If this is a linked shuttle, let the main shuttle handle the FTL
        if (comp.LinkedShuttle.HasValue)
            return;
        var shuttle = entity.Comp2;
        comp.StateTime = StartEndTime.FromCurTime(_gameTiming, DefaultArrivalTime);
        comp.State = FTLState.Arriving;

        // Create visualizer if it doesn't exist
        if (comp.VisualizerProto != null && comp.VisualizerEntity == null)
        {
            comp.VisualizerEntity = SpawnAttachedTo(entity.Comp1.VisualizerProto, entity.Comp1.TargetCoordinates);
            DebugTools.Assert(Transform(comp.VisualizerEntity.Value).ParentUid == entity.Comp1.TargetCoordinates.EntityId);
            var visuals = Comp<FtlVisualizerComponent>(comp.VisualizerEntity.Value);
            visuals.Grid = entity.Owner;
            Dirty(comp.VisualizerEntity.Value, visuals);
            _transform.SetLocalRotation(comp.VisualizerEntity.Value, comp.TargetAngle);
            _pvs.AddGlobalOverride(comp.VisualizerEntity.Value);
        }

        _thruster.DisableLinearThrusters(shuttle);
        _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.South);

        _console.RefreshShuttleConsoles(entity.Owner);
    }

    // Mono Begin
    private void MassAdjustFTLCooldown(PhysicsComponent shuttlePhysics, FTLDriveComponent drive, out float massAdjustedCooldown)
    {
        if (drive.MassAffectedDrive == false)
        {
            massAdjustedCooldown = drive.Cooldown;
            return;
        }
        var adjustedMass = shuttlePhysics.Mass * drive.DriveMassMultiplier;
        var massMultiplier = float.Log(float.Sqrt(adjustedMass / MassConstant + float.E));
        massMultiplier = float.Clamp(massMultiplier, MassMultiplierMin, MassMultiplierMax);
        massAdjustedCooldown = drive.Cooldown * massMultiplier;
    }
    // Mono End

    /// <summary>
    ///  Shuttle arrived.
    /// </summary>
    private void UpdateFTLArriving(Entity<FTLComponent, ShuttleComponent> entity)
    {
        var globalFtlCooldown = 10f;
        var uid = entity.Owner;
        var comp = entity.Comp1;
        // If this is a linked shuttle, let the main shuttle handle the arrival
        if (comp.LinkedShuttle.HasValue)
            return;
        var xform = _xformQuery.GetComponent(uid);
        var body = _physicsQuery.GetComponent(uid);
        DoTheDinosaur(xform);
        _dockSystem.SetDockBolts(entity, false);

        if (TryGetFTLDrive(entity, out _, out var globalDrive))
        {
            MassAdjustFTLCooldown(body, globalDrive, out var massAdjustedCooldown);
            globalFtlCooldown = massAdjustedCooldown;
        }


        // Get all docked shuttles
        var dockedShuttles = new HashSet<EntityUid>();
        GetAllDockedShuttles(uid, dockedShuttles);
        // Store relative positions and docking info before moving main shuttle
        var relativeTransforms = new Dictionary<EntityUid, (Vector2 Position, Angle Rotation, List<(EntityUid DockA, EntityUid DockB)> Docks)>();
        foreach (var dockedUid in dockedShuttles)
        {
            if (dockedUid == uid) continue;

            var dockedXform = _xformQuery.GetComponent(dockedUid);
            var mainPos = _transform.GetWorldPosition(uid);
            var dockedPos = _transform.GetWorldPosition(dockedUid);
            var mainRot = _transform.GetWorldRotation(uid);
            var dockedRot = _transform.GetWorldRotation(dockedUid);
            var dockedBody = _physicsQuery.GetComponent(dockedUid);

            // Store position and rotation relative to main shuttle
            var dockConnections = new List<(EntityUid DockA, EntityUid DockB)>();
            var docks = _dockSystem.GetDocks(dockedUid);
            foreach (var dock in docks)
            {
                if (!TryComp<DockingComponent>(dock, out var dockComp) || !dockComp.Docked || dockComp.DockedWith == null)
                    continue;
                dockConnections.Add((dock, dockComp.DockedWith.Value));
                _dockSystem.Undock((dock, dockComp));
            }
            relativeTransforms[dockedUid] = (dockedPos - mainPos, dockedRot - mainRot, dockConnections);
            _physics.SetLinearVelocity(dockedUid, Vector2.Zero, body: dockedBody);
            _physics.SetAngularVelocity(dockedUid, 0f, body: dockedBody);
        }

        // Handle physics for main shuttle
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
        _physics.SetAngularVelocity(uid, 0f, body: body);

        var target = comp.TargetCoordinates;
        MapId mapId;

        QueueDel(comp.VisualizerEntity);
        comp.VisualizerEntity = null;

        if (!Exists(comp.TargetCoordinates.EntityId))
        {
            // Uhh good luck
            // Pick earliest map?
            var maps = EntityQuery<MapComponent>().Select(o => o.MapId).ToList();
            var map = maps.Min(o => o.GetHashCode());
            mapId = new MapId(map);
            TryFTLProximity(uid, _mapSystem.GetMap(mapId));
        }
        // Docking FTL
        else if (HasComp<MapGridComponent>(target.EntityId) && !HasComp<MapComponent>(target.EntityId))
        {
            var config = _dockSystem.GetDockingConfigAt(uid, target.EntityId, target, comp.TargetAngle);
            var mapCoordinates = _transform.ToMapCoordinates(target);

            // Couldn't dock somehow so just fallback to regular position FTL.
            if (config == null)
            {
                TryFTLProximity(uid, target.EntityId);
            }
            else
            {
                FTLDock((uid, xform), config);
            }

            mapId = mapCoordinates.MapId;
        }
        // Position ftl
        else
        {
            // TODO: This should now use tryftlproximity
            mapId = _transform.GetMapId(target);
            _transform.SetCoordinates(uid, xform, target, rotation: comp.TargetAngle.Reduced());
        }

        var handledShuttles = new HashSet<EntityUid>();
        // Now move all docked shuttles to maintain their relative positions
        var mainNewPos = _transform.GetWorldPosition(uid);
        var mainNewRot = _transform.GetWorldRotation(uid);
        foreach (var dockedUid in dockedShuttles)
        {
            if (dockedUid == uid) continue;
            var dockedXform = _xformQuery.GetComponent(dockedUid);
            var (relativePos, relativeRot, dockConnections) = relativeTransforms[dockedUid];
            var ftlCooldown = 10f;

            var newPos = mainNewPos + relativePos;
            var newRot = mainNewRot + relativeRot;
            if (xform.MapUid != null)
            {
                _transform.SetParent(dockedUid, dockedXform, xform.MapUid.Value);
                _transform.SetWorldRotationNoLerp(dockedUid, newRot);
                _transform.SetWorldPosition(dockedUid, newPos);

            }
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(uid, 0f, body: body);

            if (TryComp<PhysicsComponent>(dockedUid, out var dockedBody))
            {
                _physics.SetLinearVelocity(dockedUid, Vector2.Zero, body: dockedBody);
                _physics.SetAngularVelocity(dockedUid, 0f, body: dockedBody);
                var dockedShuttle = Comp<ShuttleComponent>(dockedUid);
                if (TryGetFTLDrive(dockedUid, out _, out var drive))
                {
                    MassAdjustFTLCooldown(dockedBody, drive, out var massAdjustedCooldown);
                    ftlCooldown = massAdjustedCooldown;
                }
                if (HasComp<MapGridComponent>(xform.MapUid))
                {
                    Disable(dockedUid, component: dockedBody);
                }
                else
                {
                    Enable(dockedUid, component: dockedBody, shuttle: dockedShuttle);
                }
            }

            // Re-establish all docking connections
            foreach (var (dockA, dockB) in dockConnections)
            {
                if (!TryComp<DockingComponent>(dockA, out var dockCompA) ||
                    !TryComp<DockingComponent>(dockB, out var dockCompB))
                    continue;
                _dockSystem.Dock((dockA, dockCompA), (dockB, dockCompB));
                _dockSystem.Dock((dockB, dockCompB), (dockA, dockCompA));
            }

            // Put linked shuttles in cooldown state instead of immediately removing the component
            if (ftlCooldown > 0f && TryComp<FTLComponent>(dockedUid, out var dockedFtl))
            {
                dockedFtl.State = FTLState.Cooldown;
                dockedFtl.StateTime = StartEndTime.FromCurTime(_gameTiming, ftlCooldown);
            }
            else
            {
                RemComp<FTLComponent>(dockedUid);
            }

            // Refresh consoles for this docked shuttle as well
            _console.RefreshShuttleConsoles(dockedUid);
        }

        // Only remove visualizer after everything is in position
        QueueDel(comp.VisualizerEntity);
        comp.VisualizerEntity = null;
        _thruster.DisableLinearThrusters(entity.Comp2);

        comp.TravelStream = _audio.Stop(comp.TravelStream);
        var audio = _audio.PlayPvs(_arrivalSound, uid);
        _audio.SetGridAudio(audio);

        // Re-enable map if it was paused.
        if (TryComp<FTLDestinationComponent>(_mapManager.GetMapEntityId(mapId), out var dest))
        {
            dest.Enabled = true;
        }

        _mapManager.SetMapPaused(mapId, false);
        Smimsh(uid, xform: xform);

        // Add cooldown before removing the FTL component
        if (globalFtlCooldown > 0f)
        {
            comp.State = FTLState.Cooldown;
            comp.StateTime = StartEndTime.FromCurTime(_gameTiming, globalFtlCooldown);
        }
        else
        {
            RemComp(uid, comp);
        }

        var ftlEvent = new FTLCompletedEvent(uid, _mapSystem.GetMap(mapId));
        RaiseLocalEvent(uid, ref ftlEvent, true);
        _console.RefreshShuttleConsoles(uid);

        if (_physicsQuery.TryGetComponent(uid, out body))
        {
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(uid, 0f, body: body);

            // Disable shuttle if it's on a planet; unfortunately can't do this in parent change messages due
            // to event ordering and awake body shenanigans (at least for now).
            if (HasComp<MapGridComponent>(xform.MapUid))
            {
                Disable(uid, component: body);
            }
            else
            {
                Enable(uid, component: body, shuttle: entity.Comp2);
            }
        }
    }

    private void UpdateFTLCooldown(Entity<FTLComponent, ShuttleComponent> entity)
    {
        var uid = entity.Owner;
        RemCompDeferred<FTLComponent>(entity);

        // Find any docked shuttles that might still be in cooldown from the same FTL trip
        // and force them to also end cooldown at the same time
        var linkedQuery = EntityQueryEnumerator<FTLComponent>();
        while (linkedQuery.MoveNext(out var linkedUid, out var linkedComp))
        {
            if (linkedComp.LinkedShuttle == uid && linkedComp.State == FTLState.Cooldown)
            {
                RemCompDeferred<FTLComponent>(linkedUid);
                _console.RefreshShuttleConsoles(linkedUid);
            }
        }

        _console.RefreshShuttleConsoles(uid);
    }

    private void UpdateHyperspace()
    {
        var curTime = _gameTiming.CurTime;

        // Create a list to store entities that need to be processed to avoid collection modification issues
        var entitiesToProcess = new List<(EntityUid Uid, FTLComponent Comp, ShuttleComponent Shuttle)>();

        // First, gather all entities to process
        var query = EntityQueryEnumerator<FTLComponent, ShuttleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var shuttle))
        {
            if (curTime >= comp.StateTime.End)
            {
                entitiesToProcess.Add((uid, comp, shuttle));
            }
        }

        // Then process them separately to avoid modifying the collection during enumeration
        foreach (var (uid, comp, shuttle) in entitiesToProcess)
        {
            var entity = (uid, comp, shuttle);

            switch (comp.State)
            {
                // Startup time has elapsed and in hyperspace.
                case FTLState.Starting:
                    UpdateFTLStarting(entity);
                    break;
                // Arriving, play effects
                case FTLState.Travelling:
                    UpdateFTLTravelling(entity);
                    break;
                // Arrived
                case FTLState.Arriving:
                    UpdateFTLArriving(entity);
                    break;
                case FTLState.Cooldown:
                    UpdateFTLCooldown(entity);
                    break;
                default:
                    Log.Error($"Found invalid FTL state {comp.State} for {uid}");
                    RemCompDeferred<FTLComponent>(uid);
                    break;
            }
        }
    }

    private float GetSoundRange(EntityUid uid)
    {
        if (!TryComp<MapGridComponent>(uid, out var grid))
            return 4f;

        return MathF.Max(grid.LocalAABB.Width, grid.LocalAABB.Height) + 12.5f;
    }

    /// <summary>
    /// Puts everyone unbuckled on the floor, paralyzed.
    /// </summary>
    private void DoTheDinosaur(TransformComponent xform)
    {
        // Get enumeration exceptions from people dropping things if we just paralyze as we go
        var toKnock = new ValueList<EntityUid>();
        KnockOverKids(xform, ref toKnock);
        TryComp<MapGridComponent>(xform.GridUid, out var grid);

        if (TryComp<PhysicsComponent>(xform.GridUid, out var shuttleBody))
        {
            foreach (var child in toKnock)
            {
                if (!_statusQuery.TryGetComponent(child, out var status))
                    continue;

                if (!HasComp<FTLKnockdownImmuneComponent>(child)) // Frontier: FTL knockdown immunity
                    // goob edit - stunmeta
                    _stuns.TryKnockdown(child, _hyperspaceKnockdownTime, true, status);

                // If the guy we knocked down is on a spaced tile, throw them too
                if (grid != null)
                    TossIfSpaced((xform.GridUid.Value, grid, shuttleBody), child);
            }
        }
    }

    private void LeaveNoFTLBehind(Entity<TransformComponent> grid, Matrix3x2 oldGridMatrix, EntityUid? oldMapUid)
    {
        if (oldMapUid == null)
            return;

        _noFtls.Clear();
        var oldGridRotation = oldGridMatrix.Rotation();
        _lookup.GetGridEntities(grid.Owner, _noFtls);

        foreach (var childUid in _noFtls)
        {
            if (!_xformQuery.TryComp(childUid, out var childXform))
                continue;

            // If we're not parented directly to the grid the matrix may be wrong.
            var relative = _physics.GetRelativePhysicsTransform(childUid.Owner, (grid.Owner, grid.Comp));

            _transform.SetCoordinates(
                childUid,
                childXform,
                new EntityCoordinates(oldMapUid.Value,
                Vector2.Transform(relative.Position, oldGridMatrix)), rotation: relative.Quaternion2D.Angle + oldGridRotation);
        }
    }

    private void KnockOverKids(TransformComponent xform, ref ValueList<EntityUid> toKnock)
    {
        // Not recursive because probably not necessary? If we need it to be that's why this method is separate.
        var childEnumerator = xform.ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (!_buckleQuery.TryGetComponent(child, out var buckle) || buckle.Buckled)
                continue;

            toKnock.Add(child);
        }
    }

    /// <summary>
    /// Throws people who are standing on a spaced tile, tries to throw them towards a neighbouring space tile
    /// </summary>
    private void TossIfSpaced(Entity<MapGridComponent, PhysicsComponent> shuttleEntity, EntityUid tossed)
    {
        var shuttleGrid = shuttleEntity.Comp1;
        var shuttleBody = shuttleEntity.Comp2;
        if (!_xformQuery.TryGetComponent(tossed, out var childXform))
            return;

        // only toss if its on lattice/space
        var tile = _mapSystem.GetTileRef(shuttleEntity, shuttleGrid, childXform.Coordinates);

        if (!tile.IsSpace(_tileDefManager))
            return;

        var throwDirection = childXform.LocalPosition - shuttleBody.LocalCenter;

        if (throwDirection == Vector2.Zero)
            return;

        _throwing.TryThrow(tossed, throwDirection.Normalized() * 10.0f, 50.0f);
    }

    /// <summary>
    /// Tries to dock with the target grid, otherwise falls back to proximity.
    /// This bypasses FTL travel time.
    /// </summary>
    public bool TryFTLDock(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityUid targetUid,
        string? priorityTag = null,
        DockType dockType = DockType.Airlock) // Frontier
    {
        return TryFTLDock(shuttleUid, component, targetUid, out _, priorityTag, dockType); // Frontier: add dockType
    }

    /// <summary>
    /// Tries to dock with the target grid, otherwise falls back to proximity.
    /// This bypasses FTL travel time.
    /// </summary>
    public bool TryFTLDock(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityUid targetUid,
        [NotNullWhen(true)] out DockingConfig? config,
        string? priorityTag = null,
        DockType dockType = DockType.Airlock) // Frontier
    {
        config = null;

        if (!_xformQuery.TryGetComponent(shuttleUid, out var shuttleXform) ||
            !_xformQuery.TryGetComponent(targetUid, out var targetXform) ||
            targetXform.MapUid == null ||
            !targetXform.MapUid.Value.IsValid())
        {
            return false;
        }

        config = _dockSystem.GetDockingConfig(shuttleUid, targetUid, priorityTag, dockType); // Frontier: add dockType

        if (config != null)
        {
            FTLDock((shuttleUid, shuttleXform), config);
            return true;
        }

        TryFTLProximity(shuttleUid, targetUid, shuttleXform, targetXform);
        return false;
    }

    /// <summary>
    /// Forces an FTL dock.
    /// </summary>
    public void FTLDock(Entity<TransformComponent> shuttle, DockingConfig config)
    {
        // Set position
        var mapCoordinates = _transform.ToMapCoordinates(config.Coordinates);
        var mapUid = _mapSystem.GetMap(mapCoordinates.MapId);
        _transform.SetCoordinates(shuttle.Owner, shuttle.Comp, new EntityCoordinates(mapUid, mapCoordinates.Position), rotation: config.Angle + _transform.GetWorldRotation(config.Coordinates.EntityId));

        // Connect everything
        foreach (var (dockAUid, dockBUid, dockA, dockB) in config.Docks)
        {
            _dockSystem.Dock((dockAUid, dockA), (dockBUid, dockB));
        }
    }

    /// <summary>
    /// Tries to get the target position to FTL near the target coordinates.
    /// If the target coordinates have a mapgrid then will try to offset the AABB.
    /// </summary>
    /// <param name="minOffset">Min offset for the final FTL.</param>
    /// <param name="maxOffset">Max offset for the final FTL from the box we spawn.</param>
    private bool TryGetFTLProximity(
        EntityUid shuttleUid,
        EntityCoordinates targetCoordinates,
        out EntityCoordinates coordinates, out Angle angle,
        float minOffset = 0f, float maxOffset = 64f,
        TransformComponent? xform = null, TransformComponent? targetXform = null)
    {
        DebugTools.Assert(minOffset < maxOffset);
        coordinates = EntityCoordinates.Invalid;
        angle = Angle.Zero;

        if (!Resolve(targetCoordinates.EntityId, ref targetXform) ||
            targetXform.MapUid == null ||
            !targetXform.MapUid.Value.IsValid() ||
            !Resolve(shuttleUid, ref xform))
        {
            return false;
        }

        // We essentially expand the Box2 of the target area until nothing else is added then we know it's valid.
        // Can't just get an AABB of every grid as we may spawn very far away.
        //var nearbyGrids = new HashSet<EntityUid>(); // Frontier
        var shuttleAABB = Comp<MapGridComponent>(shuttleUid).LocalAABB;

        // Start with small point.
        // If our target pos is offset we mot even intersect our target's AABB so we don't include it.
        var targetLocalAABB = Box2.CenteredAround(targetCoordinates.Position, Vector2.One);

        // How much we expand the target AABB be.
        // We half it because we only need the width / height in each direction if it's placed at a particular spot.
        var expansionAmount = MathF.Max(shuttleAABB.Width * 0.72f, shuttleAABB.Height * 0.72f); // Frontier: "/ 2" < "* 0.72" - a bit over sqrt 2, worst case for AABB shenanigans

        // Expand the starter AABB so we have something to query to start with.
        var targetAABB = _transform.GetWorldMatrix(targetXform)
            .TransformBox(targetLocalAABB)
            .Enlarged(expansionAmount);

        // Frontier: our world is very dense in places, very sparse overall, and very large.
        // Running a mapwise union results in ships sent very far away.
        var iteration = 0;
        var grids = new List<Entity<MapGridComponent>>();
        const float minMargin = 8.0f;
        const float maxMargin = 32.0f;

        // Pick a cardinal direction to move in.
        // true: axis-positive movement
        // false: axis-negative movement
        // null: no movement in axis
        var direction = _random.Next(8);
        bool? positiveX;
        bool? positiveY;
        // Nasty but readable
        switch (direction)
        {
            case 0:
            default:
                positiveX = true;
                positiveY = null;
                break;
            case 1:
                positiveX = true;
                positiveY = true;
                break;
            case 2:
                positiveX = null;
                positiveY = true;
                break;
            case 3:
                positiveX = false;
                positiveY = true;
                break;
            case 4:
                positiveX = false;
                positiveY = null;
                break;
            case 5:
                positiveX = false;
                positiveY = false;
                break;
            case 6:
                positiveX = null;
                positiveY = false;
                break;
            case 7:
                positiveX = true;
                positiveY = false;
                break;
        }
        while (iteration < FTLProximityIterations)
        {
            grids.Clear();
            _mapManager.FindGridsIntersecting(targetXform.MapID, targetAABB, ref grids);
            if (grids.Count == 0)
                break;

            // Adjust our requested position to be clear of intersecting grids along our randomly chosen direction.
            foreach (var grid in grids)
            {
                var collidingBox = _transform.GetWorldMatrix(grid).TransformBox(Comp<MapGridComponent>(grid).LocalAABB);

                if (positiveX == true)
                {
                    var newLeft = Math.Max(targetAABB.Left, collidingBox.Right + _random.NextFloat(minMargin, maxMargin));
                    targetAABB.Right = newLeft + targetAABB.Width;
                    targetAABB.Left = newLeft;
                }
                else if (positiveX == false)
                {
                    var newRight = Math.Min(targetAABB.Right, collidingBox.Left - _random.NextFloat(minMargin, maxMargin));
                    targetAABB.Left = newRight - targetAABB.Width;
                    targetAABB.Right = newRight;
                }
                else
                {
                    var margin = _random.NextFloat(-maxMargin, maxMargin);
                    targetAABB.Left += margin;
                    targetAABB.Right += margin;
                }

                if (positiveY == true)
                {
                    var newBottom = Math.Max(targetAABB.Bottom, collidingBox.Top + _random.NextFloat(minMargin, maxMargin));
                    targetAABB.Top = newBottom + targetAABB.Height;
                    targetAABB.Bottom = newBottom;
                }
                else if (positiveY == false)
                {
                    var newTop = Math.Min(targetAABB.Top, collidingBox.Bottom - _random.NextFloat(minMargin, maxMargin));
                    targetAABB.Bottom = newTop - targetAABB.Height;
                    targetAABB.Top = newTop;
                }
                else
                {
                    var margin = _random.NextFloat(-maxMargin, maxMargin);
                    targetAABB.Bottom += margin;
                    targetAABB.Top += margin;
                }
            }
            iteration++;
        }
        // End Frontier

        // Now we have a targetAABB. This has already been expanded to account for our fat ass.
        Vector2 spawnPos;

        if (TryComp<PhysicsComponent>(shuttleUid, out var shuttleBody))
        {
            _physics.SetLinearVelocity(shuttleUid, Vector2.Zero, body: shuttleBody);
            _physics.SetAngularVelocity(shuttleUid, 0f, body: shuttleBody);
        }

        // Frontier: spawn in our AABB
        // TODO: This should prefer the position's angle instead.
        // TODO: This is pretty crude for multiple landings.
        /*
        if (nearbyGrids.Count > 1 || !HasComp<MapComponent>(targetXform.GridUid))
        {
            // Pick a random angle
            var offsetAngle = _random.NextAngle();

            // Our valid spawn positions are <targetAABB width / height +  offset> away.
            var minRadius = MathF.Max(targetAABB.Width / 2f, targetAABB.Height / 2f);
            spawnPos = targetAABB.Center + offsetAngle.RotateVec(new Vector2(_random.NextFloat(minRadius + minOffset, minRadius + maxOffset), 0f));
        }
        else if (shuttleBody != null)
        {
            (spawnPos, angle) = _transform.GetWorldPositionRotation(targetXform);
        }
        else
        {
            spawnPos = _transform.GetWorldPosition(targetXform);
        }
        */
        spawnPos = targetAABB.Center;
        // End Frontier

        var offset = Vector2.Zero;

        // Offset it because transform does not correspond to AABB position.
        if (TryComp(shuttleUid, out MapGridComponent? shuttleGrid))
        {
            offset = -shuttleGrid.LocalAABB.Center;
        }

        if (!HasComp<MapComponent>(targetXform.GridUid))
        {
            angle = _random.NextAngle();
        }
        else
        {
            angle = Angle.Zero;
        }

        // Rotate our localcenter around so we spawn exactly where we "think" we should (center of grid on the dot).
        var transform = new Transform(spawnPos, angle);
        spawnPos = Robust.Shared.Physics.Transform.Mul(transform, offset);

        coordinates = new EntityCoordinates(targetXform.MapUid.Value, spawnPos - offset);
        return true;
    }

    /// <summary>
    /// Tries to arrive nearby without overlapping with other grids.
    /// </summary>
    public bool TryFTLProximity(EntityUid shuttleUid, EntityUid targetUid, TransformComponent? xform = null, TransformComponent? targetXform = null)
    {
        if (!Resolve(targetUid, ref targetXform) ||
            targetXform.MapUid == null ||
            !targetXform.MapUid.Value.IsValid() ||
            !Resolve(shuttleUid, ref xform))
        {
            return false;
        }

        if (!TryGetFTLProximity(shuttleUid, new EntityCoordinates(targetUid, Vector2.Zero), out var coords, out var angle, xform: xform, targetXform: targetXform))
            return false;

        _transform.SetCoordinates(shuttleUid, xform, coords, rotation: angle);
        return true;
    }

    /// <summary>
    /// Tries to FTL to the target coordinates; will move nearby if not possible.
    /// </summary>
    public bool TryFTLProximity(Entity<TransformComponent?> shuttle, EntityCoordinates targetCoordinates)
    {
        if (!Resolve(shuttle.Owner, ref shuttle.Comp) ||
            _transform.GetMap(targetCoordinates)?.IsValid() != true)
        {
            return false;
        }

        if (!TryGetFTLProximity(shuttle, targetCoordinates, out var coords, out var angle))
            return false;

        _transform.SetCoordinates(shuttle, shuttle.Comp, coords, rotation: angle);
        return true;
    }

    /// <summary>
    /// Flattens / deletes everything under the grid upon FTL.
    /// </summary>
    private void Smimsh(EntityUid uid, FixturesComponent? manager = null, MapGridComponent? grid = null, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref manager, ref grid, ref xform) || xform.MapUid == null)
            return;

        if (!TryComp(xform.MapUid, out BroadphaseComponent? lookup))
            return;

        // Flatten anything not parented to a grid.
        var transform = _physics.GetRelativePhysicsTransform((uid, xform), xform.MapUid.Value);
        var aabbs = new List<Box2>(manager.Fixtures.Count);
        var tileSet = new List<(Vector2i, Tile)>();

        foreach (var fixture in manager.Fixtures.Values)
        {
            if (xform.MapID == _ticker.DefaultMap)
                break; //Frontier - FTL is too buggy to let it just fucking gib people wtf - so we disable for frontier's z-level

            if (!fixture.Hard)
                continue;

            var aabb = fixture.Shape.ComputeAABB(transform, 0);

            // Shift it slightly
            // Create a small border around it.
            aabb = aabb.Enlarged(0.2f);
            aabbs.Add(aabb);

            // Handle clearing biome stuff as relevant.
            tileSet.Clear();
            _biomes.ReserveTiles(xform.MapUid.Value, aabb, tileSet);
            _lookupEnts.Clear();
            _immuneEnts.Clear();
            // TODO: Ideally we'd query first BEFORE moving grid but needs adjustments above.
            _lookup.GetLocalEntitiesIntersecting(xform.MapUid.Value, fixture.Shape, transform, _lookupEnts, flags: LookupFlags.Uncontained, lookup: lookup);

            foreach (var ent in _lookupEnts)
            {
                if (ent == uid || _immuneEnts.Contains(ent))
                {
                    continue;
                }

                // If it's on our grid ignore it.
                if (!_xformQuery.TryComp(ent, out var childXform) || childXform.GridUid == uid)
                {
                    continue;
                }

                // If it has the FTLSmashImmuneComponent ignore it.
                if (_immuneQuery.HasComponent(ent))
                {
                    continue;
                }

                if (_bodyQuery.TryGetComponent(ent, out var mob))
                {
                    _logger.Add(LogType.Gib, LogImpact.Extreme, $"{ToPrettyString(ent):player} got gibbed by the shuttle" +
                                                                $" {ToPrettyString(uid)} arriving from FTL at {xform.Coordinates:coordinates}");
                    var gibs = _bobby.GibBody(ent, body: mob);
                    _immuneEnts.UnionWith(gibs);
                    continue;
                }

                QueueDel(ent);
            }
        }

        var ev = new ShuttleFlattenEvent(xform.MapUid.Value, aabbs);
        RaiseLocalEvent(ref ev);
    }

    /// <summary>
    /// Transitions shuttle to FTL map.
    /// </summary>
    private void UpdateFTLStarting(Entity<FTLComponent, ShuttleComponent> entity)
    {
        var uid = entity.Owner;
        var comp = entity.Comp1;
        // If this is a linked shuttle, let the main shuttle handle the FTL
        if (comp.LinkedShuttle.HasValue)
            return;
        var xform = _xformQuery.GetComponent(entity);
        DoTheDinosaur(xform);

        comp.State = FTLState.Travelling;
        var fromMapUid = xform.MapUid;
        var fromMatrix = _transform.GetWorldMatrix(xform);
        var fromRotation = _transform.GetWorldRotation(xform);

        var grid = Comp<MapGridComponent>(uid);
        var width = grid.LocalAABB.Width;
        var ftlMap = EnsureFTLMap();
        var body = _physicsQuery.GetComponent(entity);
        var shuttleCenter = grid.LocalAABB.Center;

        // Get all docked shuttles
        var dockedShuttles = new HashSet<EntityUid>();
        GetAllDockedShuttles(uid, dockedShuttles);
        // For docked shuttles, we want to move them as a single unit
        // So we'll store their relative transforms to the main shuttle before moving
        var relativeTransforms = new Dictionary<EntityUid, (Vector2 Position, Angle Rotation)>();
        foreach (var dockedUid in dockedShuttles)
        {
            if (dockedUid == uid) continue;

            var dockedXform = _xformQuery.GetComponent(dockedUid);
            var mainPos = _transform.GetWorldPosition(uid);
            var dockedPos = _transform.GetWorldPosition(dockedUid);
            var mainRot = _transform.GetWorldRotation(uid);
            var dockedRot = _transform.GetWorldRotation(dockedUid);

            // Store position and rotation relative to main shuttle
            // We need to rotate the relative position by the inverse of the main shuttle's rotation
            var relativePos = dockedPos - mainPos;
            relativePos = (-mainRot).RotateVec(relativePos);
            var relativeRot = dockedRot - mainRot;
            relativeTransforms[dockedUid] = (relativePos, relativeRot);
        }

        // Leave audio at the old spot
        // Just so we don't clip
        if (fromMapUid != null && TryComp(comp.StartupStream, out AudioComponent? startupAudio))
        {
            var clippedAudio = _audio.PlayStatic(_startupSound, Filter.Broadcast(),
                new EntityCoordinates(fromMapUid.Value, _mapSystem.GetGridPosition(entity.Owner)), true, startupAudio.Params);

            _audio.SetPlaybackPosition(clippedAudio, entity.Comp1.StartupTime);
            if (clippedAudio != null)
                clippedAudio.Value.Component.Flags |= AudioFlags.NoOcclusion;
        }

        // Offset the start by buffer range just to avoid overlap.
        // Move main shuttle to FTL
        var ftlStart = new EntityCoordinates(ftlMap, new Vector2(_index + width / 2f, 0f) - shuttleCenter);
        // Store the matrix for the grid prior to movement. This means any entities we need to leave behind we can make sure their positions are updated.
        // Setting the entity to map directly may run grid traversal (at least at time of writing this).
        var oldMapUid = xform.MapUid;
        var oldGridMatrix = _transform.GetWorldMatrix(xform);
        // Move main shuttle to FTL and set its rotation to zero
        _transform.SetCoordinates(entity.Owner, ftlStart);
        _transform.SetWorldRotation(entity.Owner, Angle.Zero);
        LeaveNoFTLBehind((entity.Owner, xform), oldGridMatrix, oldMapUid);

        // Reset rotation so they always face the same direction.
        xform.LocalRotation = Angle.Zero;
        _index += width + Buffer;

        // Frontier: rollover coordinates
        if (_index > MaxCoord)
            _index -= CoordRollover;
        // End Frontier

        // Move all docked shuttles maintaining their relative positions
        foreach (var dockedUid in dockedShuttles)
        {
            if (dockedUid == uid) continue;
            var dockedXform = _xformQuery.GetComponent(dockedUid);
            var dockedOldMapUid = dockedXform.MapUid;
            var dockedOldGridMatrix = _transform.GetWorldMatrix(dockedXform);
            var (relativePos, relativeRot) = relativeTransforms[dockedUid];
            var mainPos = _transform.GetWorldPosition(uid);
            var mainRot = _transform.GetWorldRotation(uid);
            // Apply the same relative transform in FTL space
            // We need to rotate the relative position by the main shuttle's new rotation
            var rotatedRelativePos = mainRot.RotateVec(relativePos);
            var newPos = mainPos + rotatedRelativePos;
            var newRot = mainRot + relativeRot;
            // Ensure we move to the same map as the main shuttle
            _transform.SetParent(dockedUid, dockedXform, ftlMap);
            _transform.SetWorldRotationNoLerp(dockedUid, newRot);
            _transform.SetWorldPosition(dockedUid, newPos);
            LeaveNoFTLBehind((dockedUid, dockedXform), dockedOldGridMatrix, dockedOldMapUid);

            // Add FTL component to the docked shuttle and link it to the main shuttle
            var dockedComp = EnsureComp<FTLComponent>(dockedUid);
            dockedComp.LinkedShuttle = uid;
            dockedComp.State = FTLState.Travelling;
            dockedComp.TargetAngle = comp.TargetAngle + relativeRot;

            if (TryComp<PhysicsComponent>(dockedUid, out var dockedBody))
            {
                Enable(dockedUid, component: dockedBody);
                _physics.SetLinearVelocity(dockedUid, new Vector2(0f, 20f), body: dockedBody);
                _physics.SetAngularVelocity(dockedUid, 0f, body: dockedBody);
            }

            // Refresh consoles for this docked shuttle as well
            _console.RefreshShuttleConsoles(dockedUid);
        }

        comp.StateTime = StartEndTime.FromCurTime(_gameTiming, comp.TravelTime - DefaultArrivalTime);

        Enable(uid, component: body);
        _physics.SetLinearVelocity(uid, new Vector2(0f, 20f), body: body);
        _physics.SetAngularVelocity(uid, 0f, body: body);

        _dockSystem.SetDockBolts(uid, true);
        _console.RefreshShuttleConsoles(uid);

        var ev = new FTLStartedEvent(uid, comp.TargetCoordinates, fromMapUid, fromMatrix, fromRotation);
        RaiseLocalEvent(uid, ref ev, true);

        // Audio
        var wowdio = _audio.PlayPvs(comp.TravelSound, uid);
        comp.TravelStream = wowdio?.Entity;
        _audio.SetGridAudio(wowdio);
    }
}

using Content.Server._Mono.Ships.Systems;
using Content.Server._Mono.Shuttles.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Systems;
using Content.Shared._NF.Shuttles.Events; // Frontier
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Popups;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Tag;
using Content.Shared.Movement.Systems;
using Content.Shared.Power;
using Content.Shared.Shuttles.UI.MapObjects;
using Content.Shared.Timing;
using Robust.Server.GameObjects;
using Robust.Shared.Collections;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Content.Shared.UserInterface;
using Content.Shared.Access.Systems; // Frontier
using Content.Shared.Construction.Components; // Frontier
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Components;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Ships.Components;
using Content.Shared.Verbs;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem : SharedShuttleConsoleSystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContentEyeSystem _eyeSystem = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly CrewedShuttleSystem _crewedShuttle = default!;

    private ISawmill _sawmill = default!;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly HashSet<Entity<ShuttleConsoleComponent>> _consoles = new();

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        _sawmill = _log.GetSawmill("shuttle-console");

        InitializeDeviceLinking();

        SubscribeLocalEvent<ShuttleConsoleComponent, ComponentStartup>(OnConsoleStartup);
        SubscribeLocalEvent<ShuttleConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<ShuttleConsoleComponent, PowerChangedEvent>(OnConsolePowerChange);
        SubscribeLocalEvent<ShuttleConsoleComponent, AnchorStateChangedEvent>(OnConsoleAnchorChange);
        SubscribeLocalEvent<ShuttleConsoleComponent, ActivatableUIOpenAttemptEvent>(OnConsoleUIOpenAttempt);
        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<ShuttleConsoleFTLBeaconMessage>(OnBeaconFTLMessage);
            subs.Event<ShuttleConsoleFTLPositionMessage>(OnPositionFTLMessage);
            subs.Event<ToggleFTLLockRequestMessage>(OnToggleFTLLock);
            subs.Event<BoundUIClosedEvent>(OnConsoleUIClose);
        });

        SubscribeLocalEvent<DroneConsoleComponent, ConsoleShuttleEvent>(OnCargoGetConsole);
        SubscribeLocalEvent<DroneConsoleComponent, AfterActivatableUIOpenEvent>(OnDronePilotConsoleOpen);
        Subs.BuiEvents<DroneConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnDronePilotConsoleClose);
        });

        SubscribeLocalEvent<DockEvent>(OnDock);
        SubscribeLocalEvent<UndockEvent>(OnUndock);

        SubscribeLocalEvent<PilotComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<PilotComponent, StopPilotingAlertEvent>(OnStopPilotingAlert);

        SubscribeLocalEvent<FTLDestinationComponent, ComponentStartup>(OnFtlDestStartup);
        SubscribeLocalEvent<FTLDestinationComponent, ComponentShutdown>(OnFtlDestShutdown);

        InitializeFTL();

        InitializeNFDrone(); // Frontier: add our drone subscriptions
    }

    private void OnFtlDestStartup(EntityUid uid, FTLDestinationComponent component, ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnFtlDestShutdown(EntityUid uid, FTLDestinationComponent component, ComponentShutdown args)
    {
        RefreshShuttleConsoles();
    }

    private void OnDock(DockEvent ev)
    {
        RefreshShuttleConsoles();
    }

    private void OnUndock(UndockEvent ev)
    {
        RefreshShuttleConsoles();
    }

    /// <summary>
    /// Refreshes all the shuttle console data for a particular grid.
    /// </summary>
    public void RefreshShuttleConsoles(EntityUid gridUid)
    {
        var exclusions = new List<ShuttleExclusionObject>();
        GetExclusions(ref exclusions);
        _consoles.Clear();
        _lookup.GetChildEntities(gridUid, _consoles);
        DockingInterfaceState? dockState = null;

        foreach (var entity in _consoles)
        {
            UpdateState(entity, ref dockState);
        }
    }

    /// <summary>
    /// Refreshes all of the data for shuttle consoles.
    /// </summary>
    public void RefreshShuttleConsoles()
    {
        var exclusions = new List<ShuttleExclusionObject>();
        GetExclusions(ref exclusions);
        var query = AllEntityQuery<ShuttleConsoleComponent>();
        DockingInterfaceState? dockState = null;

        while (query.MoveNext(out var uid, out _))
        {
            UpdateState(uid, ref dockState);
        }
    }

    /// <summary>
    /// Stop piloting if the window is closed.
    /// </summary>
    private void OnConsoleUIClose(EntityUid uid, ShuttleConsoleComponent component, BoundUIClosedEvent args)
    {
        if ((ShuttleConsoleUiKey)args.UiKey != ShuttleConsoleUiKey.Key)
        {
            return;
        }

        RemovePilot(args.Actor);
    }

    private void OnConsoleUIOpenAttempt(
        EntityUid uid,
        ShuttleConsoleComponent component,
        ActivatableUIOpenAttemptEvent args)
    {
        var shuttle = _transform.GetParentUid(uid);
        var uiOpen = _crewedShuttle.AnyGunneryConsoleActiveByPlayer(shuttle, args.User);
        var forceOne = HasComp<CrewedShuttleComponent>(shuttle) && !HasComp<AdvancedPilotComponent>(args.User);

        // Crewed shuttles should not allow people to have both gunnery and shuttle consoles open.
        if (uiOpen && forceOne)
        {
            args.Cancel();
            _popup.PopupClient(Loc.GetString("shuttle-console-crewed"), args.User);
            return;
        }

        if (!TryPilot(args.User, uid))
            args.Cancel();
    }

    private void OnConsoleAnchorChange(EntityUid uid, ShuttleConsoleComponent component,
        ref AnchorStateChangedEvent args)
    {
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
    }

    private void OnConsolePowerChange(EntityUid uid, ShuttleConsoleComponent component, ref PowerChangedEvent args)
    {
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
        _shuttle.NfSetPowered(uid, component, args.Powered); // Frontier

        // Handle job slots when power changes
        HandleJobSlotsOnPowerChange(uid, component, args.Powered);
    }

    private bool TryPilot(EntityUid user, EntityUid uid)
    {
        if (!_tags.HasTag(user, "CanPilot") ||
            !TryComp<ShuttleConsoleComponent>(uid, out var component) ||
            !this.IsPowered(uid, EntityManager) ||
            !Transform(uid).Anchored ||
            !_blocker.CanInteract(user, uid))
        {
            return false;
        }

        if (!_access.IsAllowed(user, uid)) // Frontier: check access
            return false; // Frontier

        // Check if console is locked using effective lock state (considers grid-level locks)
        if (TryComp<ShuttleConsoleLockComponent>(uid, out var lockComp))
        {
            var lockSystem = EntityManager.EntitySysManager.GetEntitySystem<SharedShuttleConsoleLockSystem>();
            if (lockSystem.GetEffectiveLockState(uid, lockComp))
            {
                // _popup.PopupEntity(Loc.GetString("shuttle-console-locked"), uid, user); // Mono
                return false;
            }
        }

        var pilotComponent = EnsureComp<PilotComponent>(user);
        var console = pilotComponent.Console;

        if (console != null)
        {
            RemovePilot(user, pilotComponent);

            // This feels backwards; is this intended to be a toggle?
            if (console == uid)
                return false;
        }

        AddPilot(uid, user, component);
        return true;
    }

    private void OnGetState(EntityUid uid, PilotComponent component, ref ComponentGetState args)
    {
        args.State = new PilotComponentState(GetNetEntity(component.Console));
    }

    private void OnStopPilotingAlert(Entity<PilotComponent> ent, ref StopPilotingAlertEvent args)
    {
        if (ent.Comp.Console != null)
        {
            RemovePilot(ent);
        }
    }

    /// <summary>
    /// Handles FTL lock toggling for docked shuttles
    /// </summary>
    private void OnToggleFTLLock(EntityUid uid, ShuttleConsoleComponent component, ToggleFTLLockRequestMessage args)
    {
        // Get the console's grid (shuttle)
        var consoleXform = Transform(uid);
        var shuttleGrid = consoleXform.GridUid;

        Logger.DebugS("shuttle", $"Server received FTL lock request with {args.DockedEntities.Count} entities, enabled={args.Enabled}");

        // If the shuttleGrid is null, we can't do anything
        if (shuttleGrid == null)
        {
            Logger.DebugS("shuttle", $"Cannot toggle FTL lock: console {ToPrettyString(uid)} is not on a grid");
            return;
        }

        bool processedMainGrid = false;

        // Process each entity in the request
        foreach (var dockedEntityNet in args.DockedEntities)
        {
            var dockedEntity = GetEntity(dockedEntityNet);

            // Check if this is the main shuttle grid
            if (dockedEntity == shuttleGrid)
            {
                processedMainGrid = true;
            }

            if (TryComp<FTLLockComponent>(dockedEntity, out var ftlLock))
            {
                Logger.DebugS("shuttle", $"Setting FTL lock for {ToPrettyString(dockedEntity)} to {args.Enabled}");
                ftlLock.Enabled = args.Enabled;
                Dirty(dockedEntity, ftlLock);
            }
        }

        // If we didn't process the main grid yet, do it now
        if (!processedMainGrid && shuttleGrid != null)
        {
            if (TryComp<FTLLockComponent>(shuttleGrid, out var ftlLock))
            {
                Logger.DebugS("shuttle", $"Setting FTL lock for main grid {ToPrettyString(shuttleGrid.Value)} to {args.Enabled}");
                ftlLock.Enabled = args.Enabled;
                Dirty(shuttleGrid.Value, ftlLock);
            }
        }
    }

    /// <summary>
    /// Sets the FTL lock state of a shuttle entity.
    /// </summary>
    /// <param name="shuttleUid">The shuttle entity to modify</param>
    /// <param name="dockedEntities">List of docked entities to also modify, or empty to only modify the shuttle</param>
    /// <param name="enabled">The desired FTL lock state (true to enable, false to disable)</param>
    /// <returns>True if at least one entity was modified, false otherwise</returns>
    public bool ToggleFTLLock(EntityUid shuttleUid, List<NetEntity> dockedEntities, bool enabled)
    {
        var modified = false;

        // Modify the main shuttle if it has the component
        if (TryComp<FTLLockComponent>(shuttleUid, out var shuttleFtlLock))
        {
            shuttleFtlLock.Enabled = enabled;
            Dirty(shuttleUid, shuttleFtlLock);
            modified = true;
        }

        // Modify any docked entities if provided
        foreach (var dockedEntityNet in dockedEntities)
        {
            var dockedEntity = GetEntity(dockedEntityNet);

            if (TryComp<FTLLockComponent>(dockedEntity, out var ftlLock))
            {
                ftlLock.Enabled = enabled;
                Dirty(dockedEntity, ftlLock);
                modified = true;
            }
        }

        return modified;
    }

    /// <summary>
    /// Returns the position and angle of all dockingcomponents.
    /// </summary>
    public Dictionary<NetEntity, List<DockingPortState>> GetAllDocks()
    {
        // TODO: NEED TO MAKE SURE THIS UPDATES ON ANCHORING CHANGES!
        var result = new Dictionary<NetEntity, List<DockingPortState>>();
        var query = AllEntityQuery<DockingComponent, TransformComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform, out var metadata))
        {
            if (xform.ParentUid != xform.GridUid)
                continue;

            // Frontier: skip unanchored docks (e.g. portable gaslocks)
            if (HasComp<AnchorableComponent>(uid) && !xform.Anchored)
                continue;
            // End Frontier

            var gridDocks = result.GetOrNew(GetNetEntity(xform.GridUid.Value));

            var state = new DockingPortState()
            {
                Name = metadata.EntityName,
                Coordinates = GetNetCoordinates(xform.Coordinates),
                Angle = xform.LocalRotation,
                Entity = GetNetEntity(uid),
                GridDockedWith =
                    _xformQuery.TryGetComponent(comp.DockedWith, out var otherDockXform) ?
                    GetNetEntity(otherDockXform.GridUid) :
                    null,
                LabelName = comp.Name != null ? Loc.GetString(comp.Name) : null, // Frontier: docking labels
                RadarColor = comp.RadarColor, // Frontier
                HighlightedRadarColor = comp.HighlightedRadarColor, // Frontier
                DockType = comp.DockType, // Frontier
                ReceiveOnly = comp.ReceiveOnly, // Frontier
            };

            gridDocks.Add(state);
        }

        return result;
    }

    private void UpdateState(EntityUid consoleUid, ref DockingInterfaceState? dockState)
    {
        EntityUid? entity = consoleUid;

        var getShuttleEv = new ConsoleShuttleEvent
        {
            Console = entity,
        };

        RaiseLocalEvent(entity.Value, ref getShuttleEv);
        entity = getShuttleEv.Console;

        TryComp(entity, out TransformComponent? consoleXform);
        var shuttleGridUid = consoleXform?.GridUid;

        NavInterfaceState navState;
        ShuttleMapInterfaceState mapState;
        dockState ??= GetDockState();

        if (shuttleGridUid != null && entity != null)
        {
            navState = GetNavState(entity.Value, dockState.Docks);
            mapState = GetMapState(shuttleGridUid.Value);
        }
        else
        {
            navState = new NavInterfaceState(0f, null, null, new Dictionary<NetEntity, List<DockingPortState>>(), InertiaDampeningMode.Dampen); // Frontier: inertia dampening);
            mapState = new ShuttleMapInterfaceState(
                FTLState.Invalid,
                default,
                new List<ShuttleBeaconObject>(),
                new List<ShuttleExclusionObject>());
        }

        if (_ui.HasUi(consoleUid, ShuttleConsoleUiKey.Key))
        {
            _ui.SetUiState(consoleUid, ShuttleConsoleUiKey.Key, new ShuttleBoundUserInterfaceState(navState, mapState, dockState));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toRemove = new ValueList<(EntityUid, PilotComponent)>();
        var query = EntityQueryEnumerator<PilotComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Console == null)
                continue;

            if (!_blocker.CanInteract(uid, comp.Console))
            {
                toRemove.Add((uid, comp));
            }
        }

        foreach (var (uid, comp) in toRemove)
        {
            RemovePilot(uid, comp);
        }
    }

    protected override void HandlePilotShutdown(EntityUid uid, PilotComponent component, ComponentShutdown args)
    {
        base.HandlePilotShutdown(uid, component, args);
        RemovePilot(uid, component);
    }

    private void OnConsoleShutdown(EntityUid uid, ShuttleConsoleComponent component, ComponentShutdown args)
    {
        ClearPilots(component);
    }

    public void AddPilot(EntityUid uid, EntityUid entity, ShuttleConsoleComponent component)
    {
        if (!EntityManager.TryGetComponent(entity, out PilotComponent? pilotComponent)
        || component.SubscribedPilots.Contains(entity))
        {
            return;
        }

        _eyeSystem.SetZoom(entity, component.Zoom, ignoreLimits: true);

        component.SubscribedPilots.Add(entity);

        _alertsSystem.ShowAlert(entity, pilotComponent.PilotingAlert);

        pilotComponent.Console = uid;
        ActionBlockerSystem.UpdateCanMove(entity);
        pilotComponent.Position = EntityManager.GetComponent<TransformComponent>(entity).Coordinates;
        Dirty(entity, pilotComponent);
    }

    public void RemovePilot(EntityUid pilotUid, PilotComponent pilotComponent)
    {
        var console = pilotComponent.Console;

        if (!TryComp<ShuttleConsoleComponent>(console, out var helm))
            return;

        pilotComponent.Console = null;
        pilotComponent.Position = null;
        _eyeSystem.ResetZoom(pilotUid);

        if (!helm.SubscribedPilots.Remove(pilotUid))
            return;

        _alertsSystem.ClearAlert(pilotUid, pilotComponent.PilotingAlert);

        _popup.PopupEntity(Loc.GetString("shuttle-pilot-end"), pilotUid, pilotUid);

        if (pilotComponent.LifeStage < ComponentLifeStage.Stopping)
            EntityManager.RemoveComponent<PilotComponent>(pilotUid);
    }

    public void RemovePilot(EntityUid entity)
    {
        if (!EntityManager.TryGetComponent(entity, out PilotComponent? pilotComponent))
            return;

        RemovePilot(entity, pilotComponent);
    }

    public void ClearPilots(ShuttleConsoleComponent component)
    {
        var query = GetEntityQuery<PilotComponent>();
        while (component.SubscribedPilots.TryGetValue(0, out var pilot))
        {
            if (query.TryGetComponent(pilot, out var pilotComponent))
                RemovePilot(pilot, pilotComponent);
        }
    }

    /// <summary>
    /// Specific for a particular shuttle.
    /// </summary>
    public NavInterfaceState GetNavState(Entity<RadarConsoleComponent?, TransformComponent?> entity, Dictionary<NetEntity, List<DockingPortState>> docks)
    {
        if (!Resolve(entity, ref entity.Comp1, ref entity.Comp2, false))
            return new NavInterfaceState(SharedRadarConsoleSystem.DefaultMaxRange, null, null, docks, Shared._NF.Shuttles.Events.InertiaDampeningMode.Dampen); // Frontier: add inertia dampening

        // Get port names from the console component if available
        var portNames = new Dictionary<string, string>();
        if (TryComp<ShuttleConsoleComponent>(entity, out var consoleComp))
        {
            portNames = consoleComp.PortNames;
        }

        return GetNavState(
            entity,
            docks,
            entity.Comp2.Coordinates,
            entity.Comp2.LocalRotation,
            portNames);
    }

    public NavInterfaceState GetNavState(
        Entity<RadarConsoleComponent?, TransformComponent?> entity,
        Dictionary<NetEntity, List<DockingPortState>> docks,
        EntityCoordinates coordinates,
        Angle angle,
        Dictionary<string, string>? portNames = null)
    {
        if (!Resolve(entity, ref entity.Comp1, ref entity.Comp2, false))
            return new NavInterfaceState(SharedRadarConsoleSystem.DefaultMaxRange, GetNetCoordinates(coordinates), angle, docks, InertiaDampeningMode.Dampen); // Frontier: add inertial dampening

        return new NavInterfaceState(
            entity.Comp1.MaxRange,
            GetNetCoordinates(coordinates),
            angle,
            docks,
            _shuttle.NfGetInertiaDampeningMode(entity), // Frontier: inertia dampening
            portNames,
            entity.Comp1.Pannable, // Mono
            entity.Comp1.RelativePanning); // Mono
    }

    /// <summary>
    /// Global for all shuttles.
    /// </summary>
    /// <returns></returns>
    public DockingInterfaceState GetDockState()
    {
        var docks = GetAllDocks();
        return new DockingInterfaceState(docks);
    }

    /// <summary>
    /// Specific to a particular shuttle.
    /// </summary>
    public ShuttleMapInterfaceState GetMapState(Entity<FTLComponent?> shuttle)
    {
        FTLState ftlState = FTLState.Available;
        StartEndTime stateDuration = default;

        if (Resolve(shuttle, ref shuttle.Comp, false) && shuttle.Comp.LifeStage < ComponentLifeStage.Stopped)
        {
            ftlState = shuttle.Comp.State;
            stateDuration = _shuttle.GetStateTime(shuttle.Comp);
        }

        List<ShuttleBeaconObject>? beacons = null;
        List<ShuttleExclusionObject>? exclusions = null;
        GetBeacons(ref beacons);
        GetExclusions(ref exclusions);

        return new ShuttleMapInterfaceState(
            ftlState,
            stateDuration,
            beacons ?? new List<ShuttleBeaconObject>(),
            exclusions ?? new List<ShuttleExclusionObject>());
    }

    /// <summary>
    /// Handles job slots when shuttle console power changes.
    /// </summary>
    private void HandleJobSlotsOnPowerChange(EntityUid consoleUid, ShuttleConsoleComponent component, bool powered)
    {
        // Get the console's transform to find the grid
        if (!TryComp<TransformComponent>(consoleUid, out var consoleXform) || consoleXform.GridUid == null)
            return;

        var gridUid = consoleXform.GridUid.Value;

        // Only handle job slots for shuttles (grids with ShuttleComponent)
        if (!HasComp<ShuttleComponent>(gridUid))
            return;

        // Find the station that owns this shuttle
        var owningStation = _station.GetOwningStation(gridUid);
        if (owningStation == null)
            return;

        // Check if the grid has any powered shuttle consoles
        var hasPoweredConsole = HasPoweredShuttleConsole(gridUid);

        if (!hasPoweredConsole)
        {
            // No powered consoles
            SaveAndCloseJobSlots(gridUid, owningStation.Value);
        }
        else
        {
            // Has powered console
            RestoreJobSlots(gridUid, owningStation.Value);
        }
    }

    /// <summary>
    /// Checks if the grid has any powered shuttle consoles.
    /// </summary>
    private bool HasPoweredShuttleConsole(EntityUid gridUid)
    {
        var query = AllEntityQuery<ShuttleConsoleComponent, TransformComponent>();

        while (query.MoveNext(out var consoleUid, out _, out var xform))
        {
            // Check if this console is on our grid
            if (xform.GridUid != gridUid)
                continue;

            // Check if this console is powered
            if (this.IsPowered(consoleUid, EntityManager))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Saves the current job slots for the station and sets them all to 0 (closed).
    /// </summary>
    private void SaveAndCloseJobSlots(EntityUid gridUid, EntityUid station)
    {
        // Get or create the job slots component on the grid
        var jobSlotsComp = EnsureComp<ShuttleConsoleJobSlotsComponent>(gridUid);

        // If we already have saved slots, don't save again
        if (jobSlotsComp.SavedJobSlots.Count > 0)
            return;

        // Clear any previous saved state and set the owning station
        jobSlotsComp.SavedJobSlots.Clear();
        jobSlotsComp.OwningStation = station;

        // Get all current job slots for the station
        if (!TryComp<StationJobsComponent>(station, out var stationJobs))
            return;

        // Save current job slots
        foreach (var (jobId, slots) in stationJobs.JobList)
        {
            // Only save jobs that have slots available (not 0)
            if (slots != 0)
            {
                jobSlotsComp.SavedJobSlots[jobId] = slots;

                // Set the job slot to 0 (closed)
                _stationJobs.TrySetJobSlot(station, jobId, 0);
            }
        }
    }

    /// <summary>
    /// Restores the previously saved job slots for the station.
    /// </summary>
    private void RestoreJobSlots(EntityUid gridUid, EntityUid station)
    {
        // Get the job slots component from the grid
        if (!TryComp<ShuttleConsoleJobSlotsComponent>(gridUid, out var jobSlotsComp))
            return;

        // If no saved slots, nothing to restore
        if (jobSlotsComp.SavedJobSlots.Count == 0)
            return;

        // Verify this is for the correct station
        if (jobSlotsComp.OwningStation != station)
            return;

        // Restore all saved job slots
        foreach (var (jobId, savedSlots) in jobSlotsComp.SavedJobSlots)
        {
            if (savedSlots.HasValue)
            {
                _stationJobs.TrySetJobSlot(station, jobId, savedSlots.Value);
            }
            else
            {
                _stationJobs.MakeJobUnlimited(station, jobId);
            }
        }

        // Clear the saved state
        jobSlotsComp.SavedJobSlots.Clear();
        jobSlotsComp.OwningStation = null;
    }
}

using Content.Server.Power.EntitySystems; // Mono
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._Mono.Ships;
using Content.Shared.Popups;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Shuttles.UI.MapObjects;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedShuttleSystem _sharedShuttle = default!;

    private const float ShuttleFTLRange = 512f;
    private const float ShuttleFTLMassThreshold = 100f; // Mono: now a soft limit, ships under the limit just stop you from shorter distance

    private const float MassConstant = 50f; // Arbitrary, at this value massMultiplier = 0.65
    private const float MassMultiplierMin = 0.5f;
    private const float MassMultiplierMax = 5f;
    private void InitializeFTL()
    {
        SubscribeLocalEvent<FTLBeaconComponent, ComponentStartup>(OnBeaconStartup);
        SubscribeLocalEvent<FTLBeaconComponent, AnchorStateChangedEvent>(OnBeaconAnchorChanged);

        SubscribeLocalEvent<FTLExclusionComponent, ComponentStartup>(OnExclusionStartup);
    }

    private void OnExclusionStartup(Entity<FTLExclusionComponent> ent, ref ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnBeaconStartup(Entity<FTLBeaconComponent> ent, ref ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnBeaconAnchorChanged(Entity<FTLBeaconComponent> ent, ref AnchorStateChangedEvent args)
    {
        RefreshShuttleConsoles();
    }

    private void OnBeaconFTLMessage(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleFTLBeaconMessage args)
    {
        var beaconEnt = GetEntity(args.Beacon);
        if (!_xformQuery.TryGetComponent(beaconEnt, out var targetXform))
        {
            return;
        }

        var nCoordinates = new NetCoordinates(GetNetEntity(targetXform.ParentUid), targetXform.LocalPosition);
        if (targetXform.ParentUid == EntityUid.Invalid)
        {
            nCoordinates = new NetCoordinates(GetNetEntity(beaconEnt), targetXform.LocalPosition);
        }

        // Check target exists
        if (!_shuttle.CanFTLBeacon(nCoordinates))
        {
            return;
        }

        var angle = args.Angle.Reduced();
        var targetCoordinates = new EntityCoordinates(targetXform.MapUid!.Value, _transform.GetWorldPosition(targetXform));

        ConsoleFTL(ent, targetCoordinates, angle, targetXform.MapID);
    }

    private void OnPositionFTLMessage(Entity<ShuttleConsoleComponent> entity, ref ShuttleConsoleFTLPositionMessage args)
    {
        var mapUid = _mapSystem.GetMap(args.Coordinates.MapId);

        // If it's beacons only block all position messages.
        if (!Exists(mapUid) || _shuttle.IsBeaconMap(mapUid))
        {
            return;
        }

        var targetCoordinates = new EntityCoordinates(mapUid, args.Coordinates.Position);
        var angle = args.Angle.Reduced();
        ConsoleFTL(entity, targetCoordinates, angle, args.Coordinates.MapId);
    }

    private void GetBeacons(ref List<ShuttleBeaconObject>? beacons)
    {
        var beaconQuery = AllEntityQuery<FTLBeaconComponent>();

        while (beaconQuery.MoveNext(out var destUid, out _))
        {
            var meta = _metaQuery.GetComponent(destUid);
            var name = meta.EntityName;

            if (string.IsNullOrEmpty(name))
                name = Loc.GetString("shuttle-console-unknown");

            // Can't travel to same map (yet)
            var destXform = _xformQuery.GetComponent(destUid);
            beacons ??= new List<ShuttleBeaconObject>();
            beacons.Add(new ShuttleBeaconObject(GetNetEntity(destUid), GetNetCoordinates(destXform.Coordinates), name));
        }
    }

    private void GetExclusions(ref List<ShuttleExclusionObject>? exclusions)
    {
        var query = AllEntityQuery<FTLExclusionComponent, TransformComponent>();

        while (query.MoveNext(out var comp, out var xform))
        {
            if (!comp.Enabled)
                continue;

            exclusions ??= new List<ShuttleExclusionObject>();
            exclusions.Add(new ShuttleExclusionObject(GetNetCoordinates(xform.Coordinates), comp.Range, Loc.GetString("shuttle-console-exclusion")));
        }
    }

    /// <summary>
    /// Handles shuttle console FTLs.
    /// </summary>
    private void ConsoleFTL(Entity<ShuttleConsoleComponent> ent, EntityCoordinates targetCoordinates, Angle targetAngle, MapId targetMap)
    {
        var consoleUid = GetDroneConsole(ent.Owner);

        if (consoleUid == null)
            return;

        var shuttleUid = _xformQuery.GetComponent(consoleUid.Value).GridUid;

        if (shuttleUid == null || !TryComp(shuttleUid.Value, out ShuttleComponent? shuttleComp))
            return;

        if (shuttleComp.Enabled == false)
            return;

        // Check shuttle can even FTL
        if (!_shuttle.CanFTL(shuttleUid.Value, out var reason))
        {
            // TODO: Session popup
            return;
        }

        // Check shuttle can FTL to this target.
        if (!_shuttle.CanFTLTo(shuttleUid.Value, targetMap, ent))
        {
            return;
        }

        targetCoordinates = _shuttle.ClampCoordinatesToFTLRange(shuttleUid.Value, targetCoordinates);

        List<ShuttleExclusionObject>? exclusions = null;
        GetExclusions(ref exclusions);

        if (!_shuttle.FTLFree(shuttleUid.Value, targetCoordinates, targetAngle, exclusions))
        {
            return;
        }

        if (!TryComp(shuttleUid.Value, out PhysicsComponent? shuttlePhysics))
        {
            return;
        }

        // Check for nearby grids that are above the mass threshold
        var xform = Transform(shuttleUid.Value);
        var bounds = xform.WorldMatrix.TransformBox(Comp<MapGridComponent>(shuttleUid.Value).LocalAABB).Enlarged(ShuttleFTLRange);
        var bodyQuery = GetEntityQuery<PhysicsComponent>();
        // Keep track of docked grids to exclude them from the proximity check
        var dockedGrids = new HashSet<EntityUid>();

        // Find all docked grids by looking for DockingComponents on the shuttle
        var dockQuery = EntityQueryEnumerator<DockingComponent, TransformComponent>();
        while (dockQuery.MoveNext(out var dockUid, out var dock, out var dockXform))
        {
            // Only consider docks on our shuttle
            if (dockXform.GridUid != shuttleUid.Value || !dock.Docked || dock.DockedWith == null)
                continue;

            // If we have a docked entity, get its grid
            if (TryComp<TransformComponent>(dock.DockedWith.Value, out var dockedXform) && dockedXform.GridUid != null)
            {
                dockedGrids.Add(dockedXform.GridUid.Value);

                // Check if we're docked to another grid
                var parentGridUid = dockedXform.GridUid.Value;

                // Find all other grids docked to this parent grid
                // These should also be excluded from the proximity check so we can
                // still FTL even when other ships are docked to the same station/grid
                var parentDockQuery = EntityQueryEnumerator<DockingComponent, TransformComponent>();
                while (parentDockQuery.MoveNext(out var parentDockUid, out var parentDock, out var parentDockXform))
                {
                    // Only consider docks on the parent grid
                    if (parentDockXform.GridUid != parentGridUid || !parentDock.Docked || parentDock.DockedWith == null)
                        continue;

                    // If we have a docked entity and it's not our ship, add its grid to the exclusion list
                    if (TryComp<TransformComponent>(parentDock.DockedWith.Value, out var siblingDockedXform) &&
                        siblingDockedXform.GridUid != null &&
                        siblingDockedXform.GridUid != shuttleUid.Value)
                    {
                        dockedGrids.Add(siblingDockedXform.GridUid.Value);
                    }
                }
            }
        }

        // Mono
        foreach (var (console, consoleComp) in _lookup.GetEntitiesInRange<ShuttleConsoleComponent>(_transform.GetMapCoordinates(xform), ShuttleFTLRange))
        {
            var consoleXform = Transform(console);
            var consGrid = consoleXform.GridUid;
            if (consGrid == null ||
                consGrid == shuttleUid ||
                dockedGrids.Contains(consGrid.Value) || // Skip grids that are docked to us or to the same parent grid
                !bodyQuery.TryGetComponent(consGrid, out var body) ||
                body.Mass < ShuttleFTLMassThreshold
                    && (_transform.GetWorldPosition(consGrid.Value) - _transform.GetWorldPosition(consoleXform)).Length() > ShuttleFTLRange * body.Mass / ShuttleFTLMassThreshold ||
                !this.IsPowered(console, EntityManager))
            {
                continue;
            }

            _popup.PopupEntity(Loc.GetString("shuttle-ftl-proximity"), ent.Owner, PopupType.Medium);
            UpdateConsoles(shuttleUid.Value);
            return;
        }

        // Client sends the "adjusted" coordinates and we adjust it back to get the actual transform coordinates.
        var adjustedCoordinates = targetCoordinates.Offset(targetAngle.RotateVec(-shuttlePhysics.LocalCenter));

        var tagEv = new FTLTagEvent();
        RaiseLocalEvent(shuttleUid.Value, ref tagEv);

        var ev = new ShuttleConsoleFTLTravelStartEvent(ent.Owner);
        RaiseLocalEvent(ref ev);
        if (_sharedShuttle.TryGetFTLDrive(shuttleUid.Value, out _, out var drive)) // Mono Begin
        {
            MassAdjustFTLStart(shuttlePhysics,
                drive,
                out var massAdjustedStartupTime,
                out var massAdjustedHyperSpaceTime);
            _shuttle.FTLToCoordinates(shuttleUid.Value, shuttleComp, adjustedCoordinates, targetAngle, massAdjustedStartupTime, massAdjustedHyperSpaceTime);
        }
    }

    // Mono Begin
    private void MassAdjustFTLStart(PhysicsComponent shuttlePhysics, FTLDriveComponent drive, out float massAdjustedStartupTime, out float massAdjustedHyperSpaceTime)
    {
        if (drive.MassAffectedDrive == false)
        {
            massAdjustedHyperSpaceTime = drive.HyperSpaceTime;
            massAdjustedStartupTime = drive.StartupTime;
            return;
        }
        var adjustedMass = shuttlePhysics.Mass * drive.DriveMassMultiplier;
        var massMultiplier = float.Log(float.Sqrt(adjustedMass / MassConstant + float.E));
        massMultiplier = float.Clamp(massMultiplier, MassMultiplierMin, MassMultiplierMax);
        massAdjustedStartupTime = drive.StartupTime * massMultiplier;
        massAdjustedHyperSpaceTime = drive.HyperSpaceTime * massMultiplier;
    }
    // Mono End
    private void UpdateConsoles(EntityUid uid, ShuttleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Update pilot consoles
        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();

        while (query.MoveNext(out var consoleUid, out var console, out var xform))
        {
            if (xform.GridUid != uid)
                continue;

            UpdateConsoleState(consoleUid, console);
        }
    }

    private void UpdateConsoleState(EntityUid uid, ShuttleConsoleComponent component)
    {
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
    }
}

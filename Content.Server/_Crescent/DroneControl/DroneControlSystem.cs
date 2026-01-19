using Content.Server._Mono.Radar;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Crescent.DroneControl;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Crescent.DroneControl;

public sealed class DroneControlSystem : EntitySystem
{
    [Dependency] private readonly DeviceListSystem _deviceList = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;

    private EntityQuery<DroneControlComponent> _controlQuery;

    private HashSet<Entity<DockingComponent>> _docks = new();
    private HashSet<Entity<DroneControlComponent>> _controllers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DroneControlConsoleComponent, GetRadarSourcesEvent>(OnGetSources);
        SubscribeLocalEvent<DroneControlConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<DroneControlConsoleComponent, DeviceListUpdateEvent>(OnListUpdate);

        SubscribeLocalEvent<DroneControlConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<DroneControlConsoleComponent, DroneConsoleMoveMessage>(OnMoveMsg);
        SubscribeLocalEvent<DroneControlConsoleComponent, DroneConsoleTargetMessage>(OnTargetMsg);

        SubscribeLocalEvent<DroneControlComponent, DeviceNetworkPacketEvent>(OnPacketReceived);

        _controlQuery = GetEntityQuery<DroneControlComponent>();
    }

    private void OnGetSources(Entity<DroneControlConsoleComponent> ent, ref GetRadarSourcesEvent args)
    {
        args.Sources = new();
        args.Sources.Add(ent);

        foreach (var (name, device) in _deviceList.GetDeviceList(ent))
        {
            var xform = Transform(device);
            if (xform.GridUid == null)
                continue;
            if (!_controlQuery.HasComp(device))
                continue;

            args.Sources.Add(device);
        }
    }

    private void OnGetAltVerbs(Entity<DroneControlConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("drone-control-autolink"),
            Priority = 10,
            Act = () => TryAutolink(ent)
        });
    }

    private void OnListUpdate(Entity<DroneControlConsoleComponent> ent, ref DeviceListUpdateEvent args)
    {
        UpdateState(ent);
    }

    private void OnUIOpened(Entity<DroneControlConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is DroneConsoleUiKey)
            UpdateState(ent);
    }

    private void OnMoveMsg(Entity<DroneControlConsoleComponent> ent, ref DroneConsoleMoveMessage args)
    {
        DoTargetedDroneOrder(ent, args.SelectedDrones, DroneOrderType.Move, GetCoordinates(args.TargetCoordinates), args.Actor);
    }

    private void OnTargetMsg(Entity<DroneControlConsoleComponent> ent, ref DroneConsoleTargetMessage args)
    {
        DoTargetedDroneOrder(ent, args.SelectedDrones, DroneOrderType.Target, GetCoordinates(args.TargetCoordinates), args.Actor);
    }

    private void OnPacketReceived(Entity<DroneControlComponent> ent, ref DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out string? cmd)
            || !TryComp<HTNComponent>(ent, out var htn)
            || !args.Data.TryGetValue(DroneConsoleConstants.TargetCoords, out EntityCoordinates coords)
        )
            return;

        var blackboard = htn.Blackboard;

        if (!blackboard.TryGetValue<string>(ent.Comp.OrderKey, out var nowCmd, EntityManager) || !nowCmd.Equals(cmd))
            _htn.ShutdownPlan(htn);

        blackboard.SetValue(ent.Comp.OrderKey, cmd);
        blackboard.SetValue(ent.Comp.TargetKey, coords);
    }

    private void DoTargetedDroneOrder(Entity<DroneControlConsoleComponent> console, HashSet<NetEntity> selected, DroneOrderType order, EntityCoordinates coordinates, EntityUid actor)
    {
        if (!coordinates.TryDistance(EntityManager, Transform(console).Coordinates, out var distance))
            return;

        if (distance > (console.Comp.MaxOrderRadius ?? float.MaxValue))
        {
            _popup.PopupEntity(Loc.GetString("drone-control-out-of-range"), console, PopupType.Medium);
            return;
        }

        var command = "";
        switch (order)
        {
            case DroneOrderType.Move:
                command = DroneConsoleConstants.CommandMove;
                break;
            case DroneOrderType.Target:
                command = DroneConsoleConstants.CommandTarget;
                break;
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = command,
            [DroneConsoleConstants.TargetCoords] = coordinates
        };

        SendToSelected(console, selected, payload);
    }

    private void SendToSelected(EntityUid source, HashSet<NetEntity> selected, NetworkPayload payload)
    {
        if (!TryComp<DeviceListComponent>(source, out var devList))
            return;

        var linked = _deviceList.GetDeviceList(source, devList);

        foreach (var (name, droneUid) in linked)
        {
            if (selected.Contains(GetNetEntity(droneUid)) && TryComp<DeviceNetworkComponent>(droneUid, out var droneNet))
                _deviceNetwork.QueuePacket(source, droneNet.Address, payload);
        }
    }

    private void UpdateState(EntityUid console)
    {
        var nav = _shuttleConsole.GetNavState(console, _shuttleConsole.GetAllDocks());

        var drones = new List<(NetEntity, NetEntity)>();
        var toRemove = new List<EntityUid>();

        foreach (var (name, device) in _deviceList.GetDeviceList(console))
        {
            var xform = Transform(device);
            if (xform.GridUid == null)
                continue;

            if (!_controlQuery.HasComp(device))
            {
                toRemove.Add(device);
                continue;
            }

            drones.Add((GetNetEntity(device), GetNetEntity(xform.GridUid.Value)));
        }

        // we have non-drone devices, clean up
        if (toRemove.Count != 0)
        {
            var newList = new List<EntityUid>();
            foreach (var (name, device) in _deviceList.GetDeviceList(console))
            {
                if (!toRemove.Contains(device))
                    newList.Add(device);
            }
            _deviceList.UpdateDeviceList(console, newList);
        }

        _ui.SetUiState(console, DroneConsoleUiKey.Key, new DroneConsoleBoundUserInterfaceState(nav, drones));
    }

    public void TryAutolink(EntityUid fromEnt)
    {
        var newDrones = new List<EntityUid>();

        var xform = Transform(fromEnt);
        var shipUid = xform.GridUid;
        if (!TryComp<MapGridComponent>(shipUid, out var grid))
            return;

        _docks.Clear();
        _lookup.GetLocalEntitiesIntersecting(shipUid.Value, grid.LocalAABB, _docks);

        foreach (var dock in _docks)
        {
            if (dock.Comp.DockedWith == null)
                continue;

            var withXform = Transform(dock.Comp.DockedWith.Value);

            if (!TryComp<MapGridComponent>(withXform.GridUid, out var withGrid))
                continue;

            _controllers.Clear();
            _lookup.GetLocalEntitiesIntersecting(withXform.GridUid.Value, withGrid.LocalAABB, _controllers);
            foreach (var controller in _controllers)
            {
                if (!_controlQuery.TryComp(controller, out var controlComp) || controlComp.Autolinked)
                    continue;

                controlComp.Autolinked = true;
                newDrones.Add(controller);
            }
        }

        if (newDrones.Count != 0)
            _deviceList.UpdateDeviceList(fromEnt, newDrones, true);

        _popup.PopupEntity(Loc.GetString("drone-control-autolinked", ("count", newDrones.Count)), fromEnt, PopupType.Large);
    }
}

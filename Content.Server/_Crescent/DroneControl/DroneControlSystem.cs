using Content.Server.DeviceNetwork.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Systems;
using Content.Shared._Crescent.DroneControl;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Robust.Shared.Map;

namespace Content.Server._Crescent.DroneControl;

public sealed class DroneControlSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private readonly DeviceListSystem _deviceList = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DroneControlConsoleComponent, DeviceListUpdateEvent>(OnListUpdate);
        SubscribeLocalEvent<DroneControlConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<DroneControlConsoleComponent, DroneConsoleMoveMessage>(OnMoveMsg);
        SubscribeLocalEvent<DroneControlConsoleComponent, DroneConsoleTargetMessage>(OnTargetMsg);

        SubscribeLocalEvent<DroneControlComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
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

            if (!HasComp<DroneControlComponent>(device))
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

    // TODO: some more generic way of handling orders if we get more possible types
    // for now a generic implementation would be counterproductive by trying to adapt to requirements we don't even have yet

    private void OnMoveMsg(Entity<DroneControlConsoleComponent> ent, ref DroneConsoleMoveMessage args)
    {
        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = DroneConsoleConstants.CommandMove,
            [DroneConsoleConstants.KeyCoords] = GetCoordinates(args.TargetCoordinates)
        };

        SendToSelected(ent, args.SelectedDrones, payload);
    }

    private void OnTargetMsg(Entity<DroneControlConsoleComponent> ent, ref DroneConsoleTargetMessage args)
    {
        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = DroneConsoleConstants.CommandTarget,
            [DroneConsoleConstants.TargetCoords] = GetCoordinates(args.TargetCoordinates)
        };

        SendToSelected(ent, args.SelectedDrones, payload);
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

    private void OnPacketReceived(Entity<DroneControlComponent> ent, ref DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out var cmd)
            || !TryComp<HTNComponent>(ent, out var htn)
        )
            return;

        var blackboard = htn.Blackboard;

        // TODO: again, decide on a more generic implementation once we have more possible orders
        // and also decide on how the blackboard keys should be unhardcoded
        if (cmd == DroneConsoleConstants.CommandMove)
        {
            if (args.Data.TryGetValue(DroneConsoleConstants.KeyCoords, out EntityCoordinates coords))
            {
                blackboard.Remove<EntityCoordinates>("ShootTarget");
                blackboard.SetValue("MoveToTarget", coords);
            }
        }
        else if (cmd == DroneConsoleConstants.CommandTarget)
        {
            if (args.Data.TryGetValue(DroneConsoleConstants.TargetCoords, out EntityCoordinates target))
            {
                blackboard.Remove<EntityCoordinates>("MoveToTarget");
                blackboard.SetValue("ShootTarget", target);
            }
        }
    }
}

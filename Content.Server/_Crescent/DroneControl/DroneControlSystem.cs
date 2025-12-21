using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Systems;
using Content.Shared._Crescent.DroneControl;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Robust.Shared.Map;
using System.Numerics;

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

        SubscribeLocalEvent<DroneControlConsoleComponent, DroneConsoleMoveMessage>(OnMoveMsg);
        SubscribeLocalEvent<DroneControlConsoleComponent, DroneConsoleTargetMessage>(OnTargetMsg);

        SubscribeLocalEvent<DroneControlComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DroneControlConsoleComponent, DeviceListComponent>();
        while (query.MoveNext(out var uid, out var comp, out var devList))
        {
             if (_ui.IsUiOpen(uid, DroneConsoleUiKey.Key))
             {
                 UpdateState(uid, comp, devList);
             }
        }
    }

    private void UpdateState(EntityUid uid, DroneControlConsoleComponent comp, DeviceListComponent devList)
    {
        var nav = _shuttleConsole.GetNavState(uid, _shuttleConsole.GetAllDocks());
        var iff = _shuttleConsole.GetIFFState(uid, null);

        var drones = new List<NetEntity>();

        foreach (var (name, device) in _deviceList.GetDeviceList(uid, devList))
        {
            if (TerminatingOrDeleted(device) || !HasComp<DroneControlComponent>(device))
                continue;

            drones.Add(GetNetEntity(device));
        }

        _ui.SetUiState(uid, DroneConsoleUiKey.Key, new DroneConsoleBoundUserInterfaceState(nav, iff, drones));
    }

    private void OnMoveMsg(EntityUid uid, DroneControlConsoleComponent component, DroneConsoleMoveMessage args)
    {
        var mapId = Transform(uid).MapID;

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = DroneConsoleConstants.CommandMove,
            [DroneConsoleConstants.KeyCoords] = new MapCoordinates(args.TargetCoordinates, mapId)
        };

        SendToSelected(uid, args.SelectedDrones, payload);
    }

    private void OnTargetMsg(EntityUid uid, DroneControlConsoleComponent component, DroneConsoleTargetMessage args)
    {
        var targetUid = GetEntity(args.TargetGrid);

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = DroneConsoleConstants.CommandTarget,
            [DroneConsoleConstants.KeyEntity] = targetUid
        };

        SendToSelected(uid, args.SelectedDrones, payload);
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

    private void OnPacketReceived(EntityUid uid, DroneControlComponent component, DeviceNetworkPacketEvent args)
    {
        Log.Info($"{ToPrettyString(uid)} got packet {args.Data}");
        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out var cmd)
            || !TryComp<HTNComponent>(uid, out var htn)
        )
            return;

        var blackboard = htn.Blackboard;

        if (cmd == DroneConsoleConstants.CommandMove)
        {
            if (args.Data.TryGetValue(DroneConsoleConstants.KeyCoords, out MapCoordinates coords))
            {
                blackboard.Remove<EntityCoordinates>("ShootTarget");
                blackboard.SetValue("MoveToTarget", _transform.ToCoordinates(coords));
            }
        }
        else if (cmd == DroneConsoleConstants.CommandTarget)
        {
            if (args.Data.TryGetValue(DroneConsoleConstants.KeyEntity, out EntityUid target))
            {
                blackboard.Remove<EntityCoordinates>("MoveToTarget");
                blackboard.SetValue("ShootTarget", new EntityCoordinates(target, Vector2.Zero));
            }
        }
    }
}

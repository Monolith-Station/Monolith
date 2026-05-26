using Content.Client.Shuttles.UI;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Events;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Log;
using Robust.Shared.Map;

// Mono
using Content.Shared._Mono.Shuttles;

namespace Content.Client.Shuttles.BUI;

[UsedImplicitly]
public sealed partial class ShuttleConsoleBoundUserInterface : BoundUserInterface // Frontier: added partial
{
    [ViewVariables]
    private ShuttleConsoleWindow? _window;

    public ShuttleConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ShuttleConsoleWindow>();

        _window.RequestFTL += OnFTLRequest;
        _window.RequestBeaconFTL += OnFTLBeaconRequest;
        _window.RequestAutopilot += OnAutopilotRequest; // Mono
        _window.DockRequest += OnDockRequest;
        _window.UndockRequest += OnUndockRequest;
        _window.UndockAllRequest += OnUndockAllRequest;
        _window.ToggleFTLLockRequest += OnToggleFTLLockRequest;
        NfOpen(); // Frontier
    }

    private void OnToggleFTLLockRequest(List<NetEntity> dockEntities, bool enabled)
    {
        Logger.DebugS("shuttle", $"ShuttleConsoleBUI: Sending FTL lock request with enabled={enabled}, entities={string.Join(", ", dockEntities)}");
        SendMessage(new ToggleFTLLockRequestMessage(dockEntities, enabled));
    }

    private void OnUndockAllRequest(List<NetEntity> dockEntities)
    {
        SendMessage(new UndockAllRequestMessage(dockEntities));
    }

    private void OnUndockRequest(NetEntity entity)
    {
        SendMessage(new UndockRequestMessage()
        {
            DockEntity = entity,
        });
    }

    private void OnDockRequest(NetEntity entity, NetEntity target)
    {
        SendMessage(new DockRequestMessage()
        {
            DockEntity = entity,
            TargetDockEntity = target,
        });
    }

    private void OnFTLBeaconRequest(NetEntity ent, Angle angle)
    {
        SendMessage(new ShuttleConsoleFTLBeaconMessage()
        {
            Beacon = ent,
            Angle = angle,
        });
    }

    private void OnFTLRequest(MapCoordinates obj, Angle angle)
    {
        SendMessage(new ShuttleConsoleFTLPositionMessage()
        {
            Coordinates = obj,
            Angle = angle,
        });
    }

    // Mono
    private void OnAutopilotRequest(MapCoordinates obj, Angle angle)
    {
        SendMessage(new ShuttleConsoleAutopilotPositionMessage()
        {
            Coordinates = obj,
            Angle = angle,
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.Dispose();
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ShuttleBoundUserInterfaceState cState)
            return;

        _window?.UpdateState(Owner, cState);
    }
}

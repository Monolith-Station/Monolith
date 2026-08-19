using System.Numerics; // Forge-Change
using Content.Client.Shuttles.UI;
using Content.Shared._Forge.ShipyardService.Components; // Forge-Change
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Events;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls; // Forge-Change
using Robust.Client.UserInterface.CustomControls; // Forge-Change
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Utility; // Forge-Change

// Mono
using Content.Shared._Mono.Shuttles;

namespace Content.Client.Shuttles.BUI;

[UsedImplicitly]
public sealed partial class ShuttleConsoleBoundUserInterface : BoundUserInterface // Frontier: added partial
{
    [ViewVariables]
    private ShuttleConsoleWindow? _window;
    private DefaultWindow? _dockConfirm; // Forge-Change

    public ShuttleConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ShuttleConsoleWindow>();
        _window.BindShuttleBui(this);

        _window.RequestFTL += OnFTLRequest;
        _window.RequestBeaconFTL += OnFTLBeaconRequest;
        _window.RequestAutopilot += OnAutopilotRequest; // Mono
        _window.RequestBioScan += OnBioScanRequest; // Forge-Change - BioScan
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

    // Forge-Change-start
    private void OnDockRequest(NetEntity entity, NetEntity target, bool shipyard)
    {
        if (shipyard || IsShipyardDock(entity) || IsShipyardDock(target))
        {
            ShowShipyardDockConfirm(entity, target);
            return;
        }

        SendDockRequest(entity, target);
    }

    private bool IsShipyardDock(NetEntity netEntity)
    {
        return EntMan.TryGetEntity(netEntity, out var uid) && EntMan.HasComponent<ShipyardDockComponent>(uid);
    }

    private void SendDockRequest(NetEntity entity, NetEntity target)
    {
        SendMessage(new DockRequestMessage()
        {
            DockEntity = entity,
            TargetDockEntity = target,
        });
    }

    private void ShowShipyardDockConfirm(NetEntity entity, NetEntity target)
    {
        _dockConfirm?.Dispose();
        _dockConfirm = new DefaultWindow
        {
            Title = Loc.GetString("shipyard-service-dock-confirm-title"),
            MinSize = new Vector2(420, 160)
        };

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(12),
            SeparationOverride = 8
        };

        var label = new RichTextLabel();
        label.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString("shipyard-service-dock-confirm-text")));

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8
        };

        var yes = new Button
        {
            Text = Loc.GetString("shipyard-service-dock-confirm-yes"),
            HorizontalExpand = true
        };
        var no = new Button
        {
            Text = Loc.GetString("shipyard-service-dock-confirm-no"),
            HorizontalExpand = true
        };

        yes.OnPressed += _ =>
        {
            SendDockRequest(entity, target);
            _dockConfirm?.Close();
        };
        no.OnPressed += _ => _dockConfirm?.Close();

        buttons.AddChild(yes);
        buttons.AddChild(no);
        box.AddChild(label);
        box.AddChild(buttons);
        _dockConfirm.Contents.AddChild(box);
        _dockConfirm.OpenCentered();
    }
    // Forge-Change-end
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

    private void OnBioScanRequest(MapCoordinates obj) // Forge-Change - BioScan
    {
        SendMessage(new ShuttleConsoleBioScanPositionMessage()
        {
            Coordinates = obj,
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.Dispose();
            _dockConfirm?.Dispose(); // Forge-Change
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

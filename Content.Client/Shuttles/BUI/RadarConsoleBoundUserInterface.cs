using Content.Shared._Mono.Sensors;
using Content.Shared.Shuttles.BUIStates;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using RadarConsoleWindow = Content.Client.Shuttles.UI.RadarConsoleWindow;

namespace Content.Client.Shuttles.BUI;

[UsedImplicitly]
public sealed class RadarConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private RadarConsoleWindow? _window;

    public RadarConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<RadarConsoleWindow>();
        _window?.SetConsole(Owner);
        _window?.SensorsTab.OnEmcon += msg => SendMessage(msg); // Mono
        _window?.SensorsTab.OnArc += msg => SendMessage(msg); // Mono
        _window?.SensorsTab.OnDatalink += msg => SendMessage(msg); // Mono
        _window?.SensorsTab.OnContactLock += net => // Mono
            SendMessage(new SensorsSetLockMessage { Target = net });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not NavBoundUserInterfaceState cState)
            return;

        _window?.UpdateState(cState.State);
        _window?.SensorsTab?.UpdateState(cState.Sensors); // Mono
    }
}

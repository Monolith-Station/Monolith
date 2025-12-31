using Content.Shared._Crescent.DroneControl;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Map;

namespace Content.Client._Crescent.DroneControl;

[UsedImplicitly]
public sealed class DroneConsoleBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private DroneConsoleWindow? _window;

    public DroneConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DroneConsoleWindow>();

        _window.OnMoveOrder += OnMoveOrder;
        _window.OnAttackOrder += OnAttackOrder;
        _window.SetConsole(Owner);
    }

    private void OnMoveOrder(EntityCoordinates coord)
    {
        if (_window == null)
            return;

        var selected = _window.SelectedDrones;
        if (selected.Count == 0)
            return;

        SendMessage(new DroneConsoleMoveMessage(selected, _entMan.GetNetCoordinates(coord)));
    }

    private void OnAttackOrder(EntityCoordinates coord)
    {
        if (_window == null)
            return;

        var selected = _window.SelectedDrones;
        if (selected.Count == 0)
            return;

        SendMessage(new DroneConsoleTargetMessage(selected, _entMan.GetNetCoordinates(coord)));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DroneConsoleBoundUserInterfaceState cast)
        {
            _window?.UpdateState(cast);
        }
    }
}

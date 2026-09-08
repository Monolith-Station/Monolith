using Content.Shared._CE.Planets.Shields;
using Robust.Client.UserInterface;

namespace Content.Client._CE.Planets.Shields;

public sealed class CEShieldGeneratorBoundUserInterface : BoundUserInterface
{

    [ViewVariables]
    private CEShieldGeneratorWindow? _window;

    public CEShieldGeneratorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CEShieldGeneratorWindow>();
        _window.SetEntity(Owner);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CEShieldGeneratorBuiState buiState)
            _window?.UpdateState(buiState);
    }
}

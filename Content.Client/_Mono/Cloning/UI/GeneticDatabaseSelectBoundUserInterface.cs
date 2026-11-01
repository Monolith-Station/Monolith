using Content.Shared._Mono.Cloning;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Cloning.UI;

public sealed class GeneticDatabaseSelectBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private GeneticDatabaseSelectWindow? _window;

    public GeneticDatabaseSelectBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GeneticDatabaseSelectWindow>();
        _window.OnDatabaseSelected += id => SendMessage(new GeneticDatabaseSelectMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is GeneticDatabaseSelectBoundUserInterfaceState cast)
            _window?.Populate(cast);
    }
}
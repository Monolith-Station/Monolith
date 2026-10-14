using Content.Shared._Mono.Cloning;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Cloning.UI;

public sealed class GeneticCloningConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private GeneticCloningConsoleWindow? _window;

    public GeneticCloningConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GeneticCloningConsoleWindow>();
        _window.Title = Loc.GetString("cloning-console-window-title");

        _window.OnCloneRecord += id => SendMessage(new GeneticCloningConsoleCloneRecordMessage(id));
        _window.OnDeleteRecord += id => SendMessage(new GeneticCloningConsoleDeleteRecordMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is GeneticCloningConsoleBoundUserInterfaceState cast)
            _window?.Populate(cast);
    }
}

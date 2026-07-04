using Content.Shared._Goobstation.Factory.Filters;
using Robust.Client.UserInterface;

namespace Content.Client._Goobstation.Factory.UI;

public sealed class NameFilterBUI : BoundUserInterface
{
    private NameFilterWindow? _window;

    public NameFilterBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<NameFilterWindow>();
        _window.SetEntity(Owner);
        _window.OnSetName += name => SendPredictedMessage(new NameFilterSetNameMessage(name));
        _window.OnSetMode += mode => SendPredictedMessage(new NameFilterSetModeMessage(mode));
    }
}

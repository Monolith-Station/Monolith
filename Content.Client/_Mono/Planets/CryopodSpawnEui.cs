using Content.Client.Eui;
using Content.Client.Message;
using Content.Shared._Mono.Persistence;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Mono.Planets;

[UsedImplicitly]
public sealed class CryopodSpawnEui : BaseEui
{
    private readonly DefaultWindow _window = new();
    private readonly BoxContainer _content = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };

    public CryopodSpawnEui()
    {
        _window.Title = Loc.GetString("cryopod-respawn-confirm-title");
        _window.Contents.AddChild(_content);
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened() => _window.OpenCentered();
    public override void Closed() => _window.Close();

    public override void HandleState(EuiStateBase state)
    {
        if (state is not CryopodSpawnEuiState cryo)
            return;

        _content.RemoveAllChildren();
        foreach (var option in cryo.Options)
        {
            var button = new Button { Text = Loc.GetString("cryopod-respawn-option-job", ("planet", option.Planet), ("job", option.Job)) };
            button.OnPressed += _ => SendMessage(new CryopodSpawnChoice(option.Id));
            _content.AddChild(button);
        }

        var warning = new RichTextLabel { MaxWidth = 440 };
        warning.SetMarkup(Loc.GetString("cryopod-respawn-forfeit", ("planet", FormattedMessage.EscapeText(cryo.Planets))));
        _content.AddChild(warning);

        var confirm = new Button { Text = Loc.GetString("cryopod-respawn-continue-normal") };
        confirm.OnPressed += _ => SendMessage(new CryopodSpawnChoice(null));
        _content.AddChild(confirm);

        var cancel = new Button { Text = Loc.GetString("accept-cryo-window-deny-button") };
        cancel.OnPressed += _ => _window.Close();
        _content.AddChild(cancel);
    }
}

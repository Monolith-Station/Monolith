using System.Numerics;
using Content.Client.Eui;
using Content.Shared._Mono.Planets;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Mono.Planets;

[UsedImplicitly]
public sealed class PlanetViewerEui : BaseEui
{
    private readonly DefaultWindow _window;
    private readonly Label _text;

    public PlanetViewerEui()
    {
        _window = new DefaultWindow
        {
            Title = Loc.GetString("planet-viewer-title"),
            MinSize = new Vector2(500, 300),
            SetSize = new Vector2(850, 600),
        };
        var layout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var refresh = new Button { Text = Loc.GetString("planet-viewer-refresh") };
        refresh.OnPressed += _ => SendMessage(new PlanetViewerRefreshMessage());
        layout.AddChild(refresh);
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        _text = new Label { Text = Loc.GetString("planet-viewer-loading") };
        scroll.AddChild(_text);
        layout.AddChild(scroll);
        _window.Contents.AddChild(layout);
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened() => _window.OpenCentered();

    public override void Closed() => _window.Close();

    public override void HandleState(EuiStateBase state)
    {
        if (state is PlanetViewerEuiState planets)
            _text.Text = planets.Text;
    }
}

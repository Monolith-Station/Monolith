using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.Administration.UI.CustomControls;

public sealed class HSeparator : Control
{
    private PanelContainer _panel;

    public static readonly Color DefaultSeparatorColor = Color.FromHex("#3D4059");

    public Color Color
    {
        get => ((StyleBoxFlat)_panel.PanelOverride!).BackgroundColor;
        set => ((StyleBoxFlat)_panel.PanelOverride!).BackgroundColor = value;
    }

    public HSeparator()
    {
        _panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = DefaultSeparatorColor,
                ContentMarginBottomOverride = 2,
                ContentMarginLeftOverride = 2
            }
        };

        AddChild(_panel);
    }
}

using Content.Client.Message;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._NF.CryoSleep;
public sealed class AcceptCryoWindow : DefaultWindow
{
    public readonly Button DenyButton;
    public readonly Button AcceptButton;
    public readonly RichTextLabel RespawnWarning = new() { Visible = false, MaxWidth = 380 };

    public AcceptCryoWindow()
    {

        Title = Loc.GetString("accept-cryo-window-title");

        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children =
                    {
                        (new Label()
                        {
                            Text = Loc.GetString("accept-cryo-window-prompt-text-part")
                        }),
                        RespawnWarning,
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            Align = AlignMode.Center,
                            Children =
                            {
                                (AcceptButton = new Button
                                {
                                    Text = Loc.GetString("accept-cryo-window-accept-button"),
                                }),

                                (new Control()
                                {
                                    MinSize = new Vector2i(20, 0)
                                }),

                                (DenyButton = new Button
                                {
                                    Text = Loc.GetString("accept-cryo-window-deny-button"),
                                })
                            }
                        },
                    }
                },
            }
        });
        RespawnWarning.SetMarkup(Loc.GetString("accept-cryo-window-respawn-warning"));
    }
}

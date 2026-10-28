using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._KS14.Packets.UI;

[CommonSheetlet]
public sealed class ExecutorStyle : Sheetlet<NanotrasenStylesheet>
{
    private static readonly ResPath TextureRoot = new("/Textures/_KS14/Interface/PacketUI");

    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        var regularFont = ResCache.GetFont("/Fonts/_KS14/BaronUI/FontStack-Regular.TTF", 12);
        var smallFont = ResCache.GetFont("/Fonts/_KS14/BaronUI/FontStack-Regular.TTF", 10);

        var panel = MakeBox("panel.png");
        var borderless = MakeBox("panel_borderless.png", patch: 0);
        var blue = MakeBox("panel_blue.png", patch: 0);

        StyleRule[] rules =
        [
            E<Label>()
                .Identifier("executor")
                .Font(regularFont)
                .FontColor(Color.Black),

            E<Button>()
                .Identifier("executor")
                .Box(blue)
                .Modulate(Color.White),

            E<TextEdit>()
                .Identifier("executorBlue")
                .Font(regularFont)
                .FontColor(Color.White),

            E<TextEdit>()
                .Identifier("executor")
                .Font(smallFont)
                .FontColor(Color.Black),

            E<PanelContainer>()
                .Identifier("executor")
                .Panel(panel)
                .Modulate(Color.White),

            E<PanelContainer>()
                .Identifier("executorBorderless")
                .Panel(borderless)
                .Modulate(Color.White),

            E<PanelContainer>()
                .Identifier("executorBlue")
                .Panel(blue)
                .Modulate(Color.White),

            E<PanelContainer>().Class(StyleClass.BackgroundPanel).Panel(panel).Modulate(Color.White),
        ];

        return rules;
    }

    private StyleBoxTexture MakeBox(string texture, int patch = 16)
    {
        Logger.Info("Text!");
        var box = new StyleBoxTexture
        {
            Texture = ResCache.GetTexture(TextureRoot / texture),
        };
        box.SetPatchMargin(StyleBox.Margin.All, patch);

        return box;
    }
}

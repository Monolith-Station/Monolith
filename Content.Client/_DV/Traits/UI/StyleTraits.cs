using System.Linq;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._DV.Traits.UI;

// Mono: This is a backport of the original Sheetlet to a Stylesheet. Replace it if Sheetlets get added
public sealed class StyleTraits : StyleBase
{
    public StyleTraits(IResourceCache resCache, Stylesheet baseStyle) : base(resCache)
    {
        Stylesheet = new Stylesheet(baseStyle.Rules.Concat(GetRules(resCache)).ToArray());
    }

    public override Stylesheet Stylesheet { get; }

    private StyleRule[] GetRules(IResourceCache resCache)
    {
        // Color palette
        // sorry but the default ColorPalette just sucks in terms of ligher/darker colors
        var bgDark = Color.FromHex("#1a1a22");
        var bgMedium = Color.FromHex("#22222a");
        var bgLight = Color.FromHex("#2a2a35");
        var bgLighter = Color.FromHex("#32323e");
        var textPrimary = Color.FromHex("#e0e0e0");
        var textSecondary = Color.FromHex("#a0a0a0");
        var textMuted = Color.FromHex("#707070");
        var accentGreen = Color.FromHex("#4ade80");
        var accentYellow = Color.FromHex("#fbbf24");
        var accentRed = Color.FromHex("#f87171");
        var accentBlue = Color.FromHex("#60a5fa");

        // StyleBoxes
        var headerPanelBox = new StyleBoxFlat
        {
            BackgroundColor = bgLight,
            BorderColor = bgLighter,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        headerPanelBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var searchBarBox = new StyleBoxFlat { BackgroundColor = bgMedium };
        searchBarBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var searchInputBox = new StyleBoxFlat
        {
            BackgroundColor = bgDark,
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8
        };

        var footerPanelBox = new StyleBoxFlat
        {
            BackgroundColor = bgMedium,
            BorderColor = bgLighter,
            BorderThickness = new Thickness(0, 1, 0, 0)
        };

        var categoryHeaderBox = new StyleBoxFlat { BackgroundColor = bgLight };
        categoryHeaderBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var categoryHeaderButtonBox = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        categoryHeaderButtonBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var categoryContentBox = new StyleBoxFlat { BackgroundColor = bgMedium };

        var categoryAccentBox = new StyleBoxFlat { BackgroundColor = accentBlue };

        var entryPanelBox = new StyleBoxFlat
        {
            BackgroundColor = bgLight,
            BorderColor = bgLighter,
            BorderThickness = new Thickness(1)
        };
        entryPanelBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var entrySelectedBox = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#2a3a4a"),
            BorderColor = accentBlue,
            BorderThickness = new Thickness(1, 1, 1, 1)
        };
        entrySelectedBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var entryDisabledBox = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#1a1a22"),
            BorderColor = Color.FromHex("#2a2a2a"),
            BorderThickness = new Thickness(1)
        };
        entryDisabledBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var progressBarBgBox = new StyleBoxFlat
        {
            BackgroundColor = bgDark,
            BorderColor = bgLighter,
            BorderThickness = new Thickness(1)
        };

        var progressBarFillFull = new StyleBoxFlat { BackgroundColor = accentGreen };
        var progressBarFillPartial = new StyleBoxFlat { BackgroundColor = accentYellow };
        var progressBarFillLow = new StyleBoxFlat { BackgroundColor = accentRed };
        var progressBarFillEmpty = new StyleBoxFlat { BackgroundColor = bgDark };

        var fontPaths = new[]
        {
            "/Fonts/NotoSans/NotoSans-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"
        };

        var notoSans10 = resCache.GetFont(fontPaths, 10);
        var notoSans11 = resCache.GetFont(fontPaths, 11);
        var notoSans12 = resCache.GetFont(fontPaths, 12);
        var notoSans14 = resCache.GetFont(fontPaths, 14);

        return new StyleRule[]
        {
            // ===== HEADER PANEL =====
            Element<PanelContainer>()
                .Class("TraitsHeaderPanel")
                .Prop(PanelContainer.StylePropertyPanel, headerPanelBox),

            Element<Label>()
                .Class("TraitsTitleLabel")
                .Prop(Label.StylePropertyFont, notoSans14)
                .Prop(Label.StylePropertyFontColor, textPrimary),

            Element<Label>()
                .Class("TraitsSubtitleLabel")
                .Prop(Label.StylePropertyFont, notoSans11)
                .Prop(Label.StylePropertyFontColor, textSecondary),

            Element<Label>()
                .Class("TraitsStatLabel")
                .Prop(Label.StylePropertyFont, notoSans12)
                .Prop(Label.StylePropertyFontColor, accentBlue),

            // ===== PROGRESS BAR =====
            Element<PanelContainer>()
                .Class("TraitsProgressBarBg")
                .Prop(PanelContainer.StylePropertyPanel, progressBarBgBox),

            Element<PanelContainer>()
                .Class("TraitsProgressBarFill")
                .Prop(PanelContainer.StylePropertyPanel, progressBarFillFull),

            Element<PanelContainer>()
                .Class("TraitsProgressBarFull")
                .Prop(PanelContainer.StylePropertyPanel, progressBarFillFull),

            Element<PanelContainer>()
                .Class("TraitsProgressBarPartial")
                .Prop(PanelContainer.StylePropertyPanel, progressBarFillPartial),

            Element<PanelContainer>()
                .Class("TraitsProgressBarLow")
                .Prop(PanelContainer.StylePropertyPanel, progressBarFillLow),

            Element<PanelContainer>()
                .Class("TraitsProgressBarEmpty")
                .Prop(PanelContainer.StylePropertyPanel, progressBarFillEmpty),

            // ===== SEARCH BAR =====
            Element<PanelContainer>()
                .Class("TraitsSearchBar")
                .Prop(PanelContainer.StylePropertyPanel, searchBarBox),

            Element<LineEdit>()
                .Class("TraitsSearchInput")
                .Prop(LineEdit.StylePropertyStyleBox, searchInputBox),

            // ===== FOOTER =====
            Element<PanelContainer>()
                .Class("TraitsFooterPanel")
                .Prop(PanelContainer.StylePropertyPanel, footerPanelBox),

            Element<Label>()
                .Class("TraitsFooterText")
                .Prop(Label.StylePropertyFont, notoSans10)
                .Prop(Label.StylePropertyFontColor, textMuted),

            // ===== CATEGORY HEADER =====
            Element<PanelContainer>()
                .Class("TraitsCategoryHeader")
                .Prop(PanelContainer.StylePropertyPanel, categoryHeaderBox),

            Element<Button>()
                .Class("TraitsCategoryHeaderButton")
                .Prop(ContainerButton.StylePropertyStyleBox, categoryHeaderButtonBox),

            Element<Label>()
                .Class("TraitsCategoryExpandIcon")
                .Prop(Label.StylePropertyFont, notoSans10)
                .Prop(Label.StylePropertyFontColor, textSecondary),

            Element<Label>()
                .Class("TraitsCategoryNameLabel")
                .Prop(Label.StylePropertyFont, notoSans12)
                .Prop(Label.StylePropertyFontColor, textPrimary),

            Element<Label>()
                .Class("TraitsCategoryStatsLabel")
                .Prop(Label.StylePropertyFont, notoSans10)
                .Prop(Label.StylePropertyFontColor, textSecondary),

            Element<Label>()
                .Class("TraitsCategoryPointsLabel")
                .Prop(Label.StylePropertyFont, notoSans10)
                .Prop(Label.StylePropertyFontColor, textMuted),

            // ===== CATEGORY ACCENT =====
            Element<PanelContainer>()
                .Class("TraitsCategoryAccent")
                .Prop(PanelContainer.StylePropertyPanel, categoryAccentBox),

            // ===== CATEGORY CONTENT =====
            Element<PanelContainer>()
                .Class("TraitsCategoryContent")
                .Prop(PanelContainer.StylePropertyPanel, categoryContentBox),

            // ===== TRAIT ENTRY =====
            Element<PanelContainer>()
                .Class("TraitsEntryPanel")
                .Prop(PanelContainer.StylePropertyPanel, entryPanelBox),

            Element<PanelContainer>()
                .Class("TraitsEntryPanel", "TraitsEntrySelected")
                .Prop(PanelContainer.StylePropertyPanel, entrySelectedBox),

            // Disabled entry styling
            Element<PanelContainer>()
                .Class("TraitsEntryPanel", "TraitsEntryDisabled")
                .Prop(PanelContainer.StylePropertyPanel, entryDisabledBox)
                .Prop(Control.StylePropertyModulateSelf, new Color(1f, 1f, 1f, 0.5f)),

            Element<Label>()
                .Class("TraitsEntryNameLabel")
                .Prop(Label.StylePropertyFont, notoSans11)
                .Prop(Label.StylePropertyFontColor, textPrimary),

            Element<Label>()
                .Class("TraitsEntryCostLabel")
                .Prop(Label.StylePropertyFont, notoSans11),

            Element<RichTextLabel>()
                .Class("TraitsEntryDescriptionLabel")
                .Prop(Label.StylePropertyFont, notoSans10)
                .Prop(Label.StylePropertyFontColor, textSecondary),
        };
    }
}

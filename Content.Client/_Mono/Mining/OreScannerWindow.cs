using System.Globalization;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Content.Shared._Mono.Mining;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Mono.Mining;

public sealed class OreScannerWindow : FancyWindow
{
    public readonly OreScannerControl Map = new();
    private readonly Label _statistics = new()
    {
        MinWidth = 220,
        MaxWidth = 220,
        VerticalAlignment = VAlignment.Top,
        HorizontalAlignment = HAlignment.Left,
        Align = Label.AlignMode.Left,
        VAlign = Label.VAlignMode.Top,
        Margin = new Thickness(12, 10, 0, 0),
    };
    private OreDepositScan? _lastDeposit;
    private bool _hasStatistics;

    public OreScannerWindow()
    {
        Title = Loc.GetString("ore-scanner-title");
        var layout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 12 };
        layout.AddChild(_statistics);
        layout.AddChild(Map);
        ContentsContainer.AddChild(layout);
        Map.OnHover = UpdateStatistics;
        UpdateStatistics(null);
    }

    private void UpdateStatistics(OreDepositScan? deposit)
    {
        if (_hasStatistics && ReferenceEquals(deposit, _lastDeposit))
            return;
        _hasStatistics = true;
        _lastDeposit = deposit;
        _statistics.Text = deposit == null ? Loc.GetString("ore-scanner-hover") :
            Loc.GetString("ore-scanner-statistics", ("mineral", deposit.Name), ("remaining", deposit.Remaining),
                ("saturation", (deposit.Saturation * 100f).ToString("0.0", CultureInfo.CurrentCulture)),
                ("rate", deposit.Rate.ToString("0.00", CultureInfo.CurrentCulture)));
    }
}

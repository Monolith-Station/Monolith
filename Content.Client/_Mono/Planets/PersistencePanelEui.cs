using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Eui;
using Content.Shared._Mono.Persistence;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client._Mono.Planets;

[UsedImplicitly]
public sealed partial class PersistencePanelEui : BaseEui
{
    private readonly DefaultWindow _window = new() { MinSize = new Vector2(560, 380) };
    private readonly OptionButton _planets = new();
    private readonly BoxContainer _details = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Label _status = new();
    private PlanetPersistenceSummary[] _entries = [];
    private DefaultWindow? _confirmation;

    public PersistencePanelEui()
    {
        _window.Title = Loc.GetString("persistence-panel-title");
        _window.OnClose += () => SendMessage(new CloseEuiMessage());

        var tabs = new TabContainer();
        tabs.AddChild(CreatePlayerTab());
        var planetTab = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
        };
        planetTab.AddChild(_planets);
        planetTab.AddChild(_status);
        var scroll = new ScrollContainer { VerticalExpand = true };
        scroll.AddChild(_details);
        planetTab.AddChild(scroll);
        tabs.AddChild(planetTab);
        tabs.SetTabTitle(0, Loc.GetString("persistence-panel-player"));
        tabs.SetTabTitle(1, Loc.GetString("persistence-panel-planet"));
        tabs.CurrentTab = 1;
        _window.Contents.AddChild(tabs);

        _planets.OnItemSelected += args => ShowPlanet(args.Id);
    }

    public override void Opened() => _window.OpenCentered();
    public override void Closed()
    {
        ClearCharacters();
        _confirmation?.Close();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not PersistencePanelState panel)
            return;

        var previous = _entries.Length == 0 ? null : _entries[_planets.SelectedId].Id;
        _entries = panel.Planets;
        _status.Text = panel.Status ?? string.Empty;
        _status.Visible = panel.Status != null;
        _planets.Clear();
        _planets.Disabled = _entries.Length == 0;
        for (var i = 0; i < _entries.Length; i++)
            _planets.AddItem(_entries[i].Name, i);
        ShowPlanet(Math.Max(0, Array.FindIndex(_entries, entry => entry.Id == previous)));
    }

    private void ShowPlanet(int index)
    {
        _details.DisposeAllChildren();
        if (_entries.Length == 0)
        {
            AddLine(_details, Loc.GetString("persistence-panel-no-planets"));
            return;
        }

        _planets.SelectId(index);
        var planet = _entries[index];
        var details = AddCard();
        AddLine(details, Loc.GetString("persistence-panel-id", ("id", planet.Id)));
        AddLine(details, Loc.GetString("persistence-panel-mode", ("mode", planet.Mode.ToString())));
        var flags = planet.Flags.Length == 0
            ? Loc.GetString("persistence-panel-no-flags")
            : string.Join(", ", planet.Flags.Select(flag => flag == PlanetPersistenceFlag.AllowRespawns
                ? Loc.GetString("persistence-panel-flag-respawns")
                : flag.ToString()));
        AddLine(details, Loc.GetString("persistence-panel-flags", ("flags", flags)));
        AddLine(details, Loc.GetString(planet.Loaded ? "persistence-panel-loaded" : "persistence-panel-unloaded"));
        if (planet.Loaded)
        {
            if (planet.NextSaveAt is { } due)
                details.AddChild(new BackupCountdown(due));
            else
                AddLine(details, Loc.GetString(planet.SavesEnabled ? "persistence-panel-next-none" : "persistence-panel-next-paused"));
        }

        var toggle = new CheckBox { Text = Loc.GetString("persistence-panel-enable-saves"), Pressed = planet.SavesEnabled };
        toggle.OnToggled += args =>
        {
            toggle.Disabled = true;
            SendMessage(new SetPlanetSavesEnabled(planet.Id, args.Pressed));
        };
        details.AddChild(toggle);

        if (planet.Saves.Length == 0)
        {
            AddLine(_details, Loc.GetString("persistence-panel-no-saves"));
            return;
        }

        AddLine(_details, Loc.GetString("persistence-panel-history"));
        foreach (var save in planet.Saves)
        {
            var date = DateTimeOffset.TryParse(save.SavedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                ? time.ToLocalTime().ToString("g")
                : save.SavedAt;
            var card = AddCard();
            var header = new BoxContainer();
            header.AddChild(new Label
            {
                Text = Loc.GetString("persistence-panel-save-round", ("round", save.Round)),
                HorizontalExpand = true,
            });
            var delete = new Button { Text = Loc.GetString("persistence-panel-delete"), Disabled = !save.CanDelete };
            if (!save.CanDelete)
                delete.ToolTip = Loc.GetString("planet-save-delete-current");
            delete.OnPressed += _ => ConfirmDelete(planet, save, date);
            header.AddChild(delete);
            card.AddChild(header);
            AddLine(card, Loc.GetString("persistence-panel-save-details", ("date", date), ("mode", save.Mode),
                ("seed", save.Seed.ToString(CultureInfo.InvariantCulture)),
                ("status", Loc.GetString(save.Final ? "persistence-panel-final" : "persistence-panel-in-progress"))));
        }
    }

    private BoxContainer AddCard() => AddCard(_details);

    private static BoxContainer AddCard(BoxContainer parent)
    {
        var panel = new PanelContainer { StyleClasses = { "PanelBackgroundBaseDark" }, Margin = new Thickness(0, 4) };
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        panel.AddChild(content);
        parent.AddChild(panel);
        return content;
    }

    private void ConfirmDelete(PlanetPersistenceSummary planet, PlanetSaveSummary save, string date)
    {
        _confirmation?.Close();
        var window = new DefaultWindow { Title = Loc.GetString("persistence-panel-delete-title") };
        _confirmation = window;
        window.OnClose += () => _confirmation = null;
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        AddLine(content, Loc.GetString("persistence-panel-delete-warning", ("planet", planet.Name),
            ("round", save.Round), ("date", date)));
        var buttons = new BoxContainer { HorizontalAlignment = Control.HAlignment.Right, SeparationOverride = 8 };
        var cancel = new Button { Text = Loc.GetString("persistence-panel-cancel") };
        cancel.OnPressed += _ => window.Close();
        buttons.AddChild(cancel);
        var delete = new Button { Text = Loc.GetString("persistence-panel-delete") };
        delete.OnPressed += _ =>
        {
            SendMessage(new DeletePlanetSave(planet.Id, save.Id, save.SavedAt));
            window.Close();
        };
        buttons.AddChild(delete);
        content.AddChild(buttons);
        window.Contents.AddChild(content);
        window.OpenCentered();
    }

    private static void AddLine(BoxContainer container, string text) => container.AddChild(new Label { Text = text });

    private sealed class BackupCountdown(TimeSpan due) : Label
    {
        private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
        private long _seconds = -1;

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            var seconds = Math.Max(0, (long) Math.Ceiling((due - _timing.CurTime).TotalSeconds));
            if (_seconds == seconds)
                return;

            _seconds = seconds;
            Text = Loc.GetString("persistence-panel-next-save", ("minutes", (seconds / 60).ToString("00")),
                ("seconds", (seconds % 60).ToString("00")));
        }
    }
}

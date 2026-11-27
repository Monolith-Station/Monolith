using System.Numerics;
using Content.Client.Lobby;
using Content.Client.UserInterface.Controls;
using Content.Shared._Mono.Persistence;
using Content.Shared.Eui;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Mono.Planets;

public sealed partial class PersistencePanelEui
{
    private readonly LineEdit _username = new() { HorizontalExpand = true };
    private readonly Button _search = new();
    private readonly Label _playerStatus = new();
    private readonly BoxContainer _characters = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly Label _entryStatus = new() { Visible = false };
    private readonly Dictionary<int, (BoxContainer Items, BoxContainer Components)> _entryLists = [];
    private readonly List<EntityUid> _previews = [];
    private DefaultWindow? _entryEditor;

    private Control CreatePlayerTab()
    {
        var tab = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
        };
        var searchBar = new BoxContainer { SeparationOverride = 8 };
        _username.PlaceHolder = Loc.GetString("persistence-panel-username");
        _username.OnTextEntered += _ => SearchUser();
        _search.Text = Loc.GetString("persistence-panel-search");
        _search.OnPressed += _ => SearchUser();
        searchBar.AddChild(_username);
        searchBar.AddChild(_search);
        tab.AddChild(searchBar);
        tab.AddChild(_playerStatus);
        tab.AddChild(_entryStatus);

        var characterScroll = new ScrollContainer { VerticalExpand = true };
        characterScroll.AddChild(_characters);
        tab.AddChild(characterScroll);
        return tab;
    }

    private void SearchUser()
    {
        var username = _username.Text.Trim();
        if (_search.Disabled || username.Length is 0 or > 64)
            return;

        ClearCharacters();
        _entryStatus.Visible = false;
        _search.Disabled = true;
        _playerStatus.Text = Loc.GetString("persistence-panel-searching");
        SendMessage(new FindPersistenceUser(username));
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is PersistenceEntriesUpdated resultUpdate)
        {
            _search.Disabled = false;
            _entryStatus.Text = resultUpdate.Error;
            _entryStatus.Visible = resultUpdate.Error != null;
            if (resultUpdate.Character is { } updated && _entryLists.TryGetValue(updated.Slot, out var lists))
            {
                ShowEntries(lists.Items, updated.Items, updated.Slot, item: true);
                ShowEntries(lists.Components, updated.Components, updated.Slot, item: false);
            }
            return;
        }

        if (msg is not PersistenceUserResult result)
        {
            base.HandleMessage(msg);
            return;
        }

        ClearCharacters();
        _search.Disabled = false;
        _playerStatus.Text = result.Failed ? Loc.GetString("persistence-panel-user-error")
            : result.Username == null ? Loc.GetString("persistence-panel-user-not-found")
            : result.Characters.Length == 0 ? Loc.GetString("persistence-panel-user-no-characters", ("user", result.Username))
            : result.Username;

        if (result.Characters.Length == 0)
            return;

        var characters = AddCard(_characters);
        characters.SeparationOverride = 4;
        var ui = IoCManager.Resolve<IUserInterfaceManager>();
        var lobby = ui.GetUIController<LobbyUIController>();
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        foreach (var character in result.Characters)
        {
            var heading = new CollapsibleHeading(character.Preview.Name)
            {
                ChevronVisible = false,
                ToolTip = Loc.GetString("persistence-panel-character-slot", ("slot", character.Slot + 1)),
            };
            if (heading.Label.Parent is BoxContainer row)
            {
                var arrow = new FoldoutArrow(heading) { MinSize = new Vector2(22, 40) };
                row.AddChild(arrow);
                arrow.SetPositionInParent(0);
                if (prototypes.TryIndex<SpeciesPrototype>(character.Preview.Species, out _))
                {
                    var dummy = lobby.LoadProfileEntity(character.Preview, null, false);
                    _previews.Add(dummy);
                    var sprite = new SpriteView { MinSize = new Vector2(36, 40), OverrideDirection = Direction.South };
                    sprite.SetEntity(dummy);
                    row.AddChild(sprite);
                    sprite.SetPositionInParent(1);
                }
            }

            var tabs = new TabContainer { Margin = new Thickness(8, 4) };
            tabs.AddChild(CreateEntryTab(character, item: true, out var items));
            tabs.AddChild(CreateEntryTab(character, item: false, out var components));
            tabs.SetTabTitle(0, Loc.GetString("persistence-panel-profile-items"));
            tabs.SetTabTitle(1, Loc.GetString("persistence-panel-profile-components"));
            _entryLists.Add(character.Slot, (items, components));
            var body = new CollapsibleBody();
            var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            content.AddChild(tabs);
            if (character.Flags.Length > 0)
                AddLine(content, Loc.GetString("persistence-panel-profile-flags", ("flags", string.Join(", ", character.Flags))));
            body.AddChild(content);
            characters.AddChild(new Collapsible(heading, body));
        }
    }

    private Control CreateEntryTab(PersistenceCharacter character, bool item, out BoxContainer entries)
    {
        var tab = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var add = new Button
        {
            Text = Loc.GetString(item ? "persistence-panel-add-item" : "persistence-panel-add-component"),
            HorizontalAlignment = Control.HAlignment.Center,
        };
        add.OnPressed += _ => OpenEntryEditor(character.Slot, item);
        tab.AddChild(add);
        entries = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        ShowEntries(entries, item ? character.Items : character.Components, character.Slot, item);
        tab.AddChild(entries);
        return tab;
    }

    private void ShowEntries(BoxContainer list, PersistenceEntry[] entries, int slot, bool item)
    {
        list.DisposeAllChildren();
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var sprites = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var row = new BoxContainer { SeparationOverride = 6 };
            var name = string.IsNullOrEmpty(entry.Name) ? Loc.GetString("persistence-panel-profile-unknown") : entry.Name;
            if (item && entry.Name is { Length: > 0 } id && prototypes.TryIndex<EntityPrototype>(id, out var prototype))
            {
                row.AddChild(new TextureRect
                {
                    Texture = sprites.GetPrototypeIcon(prototype).Default,
                    MinSize = new Vector2(32, 32),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                });
                name = prototype.Name;
            }

            row.AddChild(new Label
            {
                Text = name,
                HorizontalExpand = true,
            });
            if (entry.Sticky)
            {
                row.AddChild(new Label
                {
                    Text = "(!)",
                    FontColorOverride = Color.Yellow,
                    MouseFilter = Control.MouseFilterMode.Stop,
                    ToolTip = Loc.GetString("humanoid-profile-editor-saved-item-sticky"),
                });
            }

            var copy = new Button { Text = Loc.GetString("persistence-panel-copy-data") };
            copy.OnPressed += _ => IoCManager.Resolve<IClipboardManager>().SetText(entry.Blob);
            row.AddChild(copy);
            if (item)
            {
                var index = i;
                var remove = new ConfirmButton
                {
                    Text = Loc.GetString("persistence-panel-remove-item"),
                    ConfirmationText = Loc.GetString("persistence-panel-confirm-remove-item"),
                };
                remove.OnPressed += _ =>
                {
                    if (_search.Disabled)
                        return;

                    _search.Disabled = true;
                    _entryStatus.Visible = false;
                    _entryEditor?.Close();
                    SendMessage(new RemovePersistenceItem(slot, index));
                };
                row.AddChild(remove);
            }
            list.AddChild(row);
        }
    }

    private sealed class FoldoutArrow(BaseButton heading) : Control
    {
        private readonly Vector2[] _points = new Vector2[3];

        protected override void Draw(DrawingHandleScreen handle)
        {
            var center = PixelSize / 2f;
            var angle = Angle.FromDegrees(heading.Pressed ? 90 : 0);
            _points[0] = center + angle.RotateVec(new Vector2(-3, -5) * UIScale);
            _points[1] = center + angle.RotateVec(new Vector2(4, 0) * UIScale);
            _points[2] = center + angle.RotateVec(new Vector2(-3, 5) * UIScale);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _points, Color.White);
        }
    }

    private void OpenEntryEditor(int slot, bool item)
    {
        if (_search.Disabled)
            return;

        _entryEditor?.Close();
        var window = new DefaultWindow
        {
            Title = Loc.GetString(item ? "persistence-panel-add-item" : "persistence-panel-add-component"),
            MinSize = new Vector2(440, 250),
        };
        _entryEditor = window;
        window.OnClose += () => _entryEditor = null;
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        AddLine(content, Loc.GetString("persistence-panel-entry-paste"));
        var input = new TextEdit { VerticalExpand = true };
        content.AddChild(input);
        var sticky = new CheckBox { Text = Loc.GetString("persistence-panel-entry-sticky") };
        content.AddChild(sticky);
        var save = new Button { Text = Loc.GetString("persistence-panel-entry-save"), Disabled = true };
        input.OnTextChanged += _ => save.Disabled = input.TextLength is 0 or > AddPersistenceEntry.MaxBlobLength;
        save.OnPressed += _ =>
        {
            _search.Disabled = true;
            _entryStatus.Visible = false;
            SendMessage(new AddPersistenceEntry(slot, item, Rope.Collapse(input.TextRope), sticky.Pressed));
            window.Close();
        };
        content.AddChild(save);
        window.Contents.AddChild(content);
        window.OpenCentered();
    }

    private void ClearCharacters()
    {
        _characters.DisposeAllChildren();
        _entryEditor?.Close();
        _entryLists.Clear();
        var entities = IoCManager.Resolve<IEntityManager>();
        foreach (var preview in _previews)
            entities.DeleteEntity(preview);
        _previews.Clear();
    }
}

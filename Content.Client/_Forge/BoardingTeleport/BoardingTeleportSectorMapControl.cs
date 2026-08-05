using Content.Client.Shuttles.Systems;
using Content.Client.Shuttles.UI;
using Content.Shared._Mono.Detection;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client._Forge.BoardingTeleport;

public sealed class BoardingTeleportSectorMapControl : BoxContainer
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private readonly SharedAudioSystem _audio;
    private readonly DetectionSystem _detection;
    private readonly ShuttleSystem _shuttles;
    private readonly SharedTransformSystem _xform;

    private readonly ShuttleMapControl _map;
    private readonly LineEdit _searchBox;
    private readonly BoxContainer _objects;
    private readonly Button _selectButton;
    private readonly Label _selectedLabel;
    private readonly Dictionary<MapId, List<IMapObject>> _mapObjects = new();
    private readonly List<GridMapObject> _listedObjects = new();

    private EntityUid? _console;
    private EntityUid? _shuttle;
    private GridMapObject? _selectedGrid;
    private string _searchFilter = string.Empty;

    public event Action<MapCoordinates>? TargetCoordinatesSelected;
    public event Action<NetEntity>? TargetGridSelected;

    public BoardingTeleportSectorMapControl()
    {
        IoCManager.InjectDependencies(this);

        _audio = _entManager.System<SharedAudioSystem>();
        _detection = _entManager.System<DetectionSystem>();
        _shuttles = _entManager.System<ShuttleSystem>();
        _xform = _entManager.System<SharedTransformSystem>();

        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;
        VerticalExpand = true;

        _map = new ShuttleMapControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(0, 0, 4, 0),
            ShowFTLRange = false,
            NoFTLRange = true,
            FtlMode = true,
        };
        _map.RequestFTL += (coordinates, _) => TargetCoordinatesSelected?.Invoke(coordinates);
        AddChild(_map);

        var side = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = 290,
            MaxWidth = 320,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalExpand = true,
        };

        side.AddChild(new Label
        {
            Text = Loc.GetString("boarding-teleport-sector-settings"),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4),
        });

        var scanButton = new Button
        {
            Text = Loc.GetString("boarding-teleport-sector-scan"),
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 4),
        };
        scanButton.OnPressed += _ =>
        {
            PlayScanSound();
            RebuildMapObjects();
        };
        side.AddChild(scanButton);

        _searchBox = new LineEdit
        {
            PlaceHolder = Loc.GetString("boarding-teleport-sector-search-placeholder"),
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 4),
        };
        _searchBox.OnTextChanged += args =>
        {
            _searchFilter = args.Text.Trim();
            ApplySearchFilter();
        };
        side.AddChild(_searchBox);

        _selectButton = new Button
        {
            Text = Loc.GetString("boarding-teleport-sector-select"),
            HorizontalExpand = true,
            Disabled = true,
            Margin = new Thickness(0, 0, 0, 8),
        };
        _selectButton.OnPressed += _ => SelectCurrent();
        side.AddChild(_selectButton);

        _selectedLabel = new Label
        {
            Text = Loc.GetString("boarding-teleport-sector-selected-none"),
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 8),
        };
        side.AddChild(_selectedLabel);

        side.AddChild(new Label
        {
            Text = Loc.GetString("boarding-teleport-sector-objects"),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 10, 0, 4),
        });

        _objects = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        var objectScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        objectScroll.AddChild(_objects);
        side.AddChild(objectScroll);

        AddChild(side);
    }

    public void SetConsole(EntityUid? console)
    {
        _console = console;
        _map.SetConsole(console);
    }

    public void SetShuttle(EntityUid? shuttle)
    {
        _shuttle = shuttle;
        _map.SetShuttle(shuttle);
    }

    public void RecenterOnShuttle()
    {
        if (_shuttle == null)
            return;

        var mapPos = _xform.GetMapCoordinates(_shuttle.Value);
        _map.SetMap(mapPos.MapId, mapPos.Position, recentering: true);
    }

    public void RebuildMapObjects()
    {
        _listedObjects.Clear();
        _mapObjects.Clear();
        _selectedGrid = null;
        _selectButton.Disabled = true;
        _selectedLabel.Text = Loc.GetString("boarding-teleport-sector-selected-none");

        if (_shuttle == null)
        {
            _objects.RemoveAllChildren();
            _map.SetMapObjects(_mapObjects);
            return;
        }

        // Only scan grids on the shuttle's current map — never admin arenas, dormant ADS, etc.
        var shuttleMapId = _xform.GetMapId(_shuttle.Value);
        if (shuttleMapId == MapId.Nullspace)
        {
            _objects.RemoveAllChildren();
            _map.SetMapObjects(_mapObjects);
            return;
        }

        foreach (var grid in _mapManager.GetAllGrids(shuttleMapId))
        {
            var gridUid = grid.Owner;
            if (gridUid == _shuttle)
                continue;

            if (!_entManager.TryGetComponent<TransformComponent>(gridUid, out var xform))
                continue;

            if (xform.MapID != shuttleMapId)
                continue;

            _entManager.TryGetComponent(gridUid, out IFFComponent? iff);
            var hideLabel = iff != null &&
                            (iff.Flags & IFFFlags.HideLabel) != 0x0 &&
                            gridUid != _shuttle;

            var detectionLevel = _console == null
                ? DetectionLevel.Detected
                : _detection.IsGridDetected(gridUid, _console.Value);

            var detected = detectionLevel != DetectionLevel.Undetected || !hideLabel;
            if (!detected || iff != null && (iff.Flags & IFFFlags.Hide) != 0x0)
                continue;

            var name = hideLabel
                ? detectionLevel == DetectionLevel.PartialDetected
                    ? Loc.GetString("shuttle-console-signature-infrared")
                    : _detection.HandleUnknownMassLabel(gridUid)
                : _entManager.GetComponent<MetaDataComponent>(gridUid).EntityName;

            var mapObject = new GridMapObject
            {
                Name = name,
                Entity = gridUid,
                HideButton = iff != null && (iff.Flags & IFFFlags.HideLabelAlways) != 0x0,
            };

            _mapObjects.GetOrNew(xform.MapID).Add(mapObject);

            if (!mapObject.HideButton)
                _listedObjects.Add(mapObject);
        }

        RecenterOnShuttle();
        _map.SetMapObjects(_mapObjects);
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        _objects.RemoveAllChildren();

        var filter = _searchFilter;
        var shown = 0;

        foreach (var mapObject in _listedObjects)
        {
            if (!MatchesSearch(mapObject.Name, filter))
                continue;

            AddObjectButton(mapObject);
            shown++;
        }

        if (shown == 0 && _listedObjects.Count > 0)
        {
            _objects.AddChild(new Label
            {
                Text = Loc.GetString("boarding-teleport-sector-search-empty"),
                FontColorOverride = Color.FromHex("#A8B4C8"),
            });
        }
    }

    private static bool MatchesSearch(string name, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        return name.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void PlayScanSound()
    {
        if (_console == null)
            return;

        _audio.PlayEntity(new SoundPathSpecifier("/Audio/Effects/Shuttle/radar_ping.ogg"), Filter.Local(), _console.Value, true);
    }

    private void AddObjectButton(GridMapObject mapObject)
    {
        var button = new Button
        {
            Text = mapObject.Name,
            HorizontalExpand = true,
            TextAlign = Label.AlignMode.Left,
            Margin = new Thickness(0, 0, 0, 2),
        };
        button.OnPressed += _ =>
        {
            _selectedGrid = mapObject;
            _selectButton.Disabled = false;
            _selectedLabel.Text = Loc.GetString("boarding-teleport-sector-selected-grid", ("name", mapObject.Name));

            var coordinates = _shuttles.GetMapCoordinates(mapObject);
            _map.SetMap(coordinates.MapId, coordinates.Position, recentering: true);
            _map.SetMapObjects(_mapObjects);
        };
        _objects.AddChild(button);
    }

    private void SelectCurrent()
    {
        if (_selectedGrid is not { } selected)
            return;

        TargetGridSelected?.Invoke(_entManager.GetNetEntity(selected.Entity));
    }

    public void SetSelectedTargetName(string? name)
    {
        _selectedLabel.Text = string.IsNullOrWhiteSpace(name)
            ? Loc.GetString("boarding-teleport-sector-selected-none")
            : Loc.GetString("boarding-teleport-sector-selected-grid", ("name", name));
    }
}

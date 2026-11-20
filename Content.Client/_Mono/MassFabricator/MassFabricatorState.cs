using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Viewport;
using Content.Client.Viewport;
using Content.Shared.Input;
using Content.Shared._Mono.MassFabricator;
using Content.Shared.Materials;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.GameObjects;
using Robust.Client.Placement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class MassFabricatorState : GameplayStateBase, IMainViewportState
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IEyeManager _eyes = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPlacementManager _placement = default!;

    private MassFabricatorMenu? _menu;
    private bool _rotationKeyHeld;
    private bool _singleTilePress;
    private Tooltip? _supportTooltip;
    private string? _supportTooltipKey;

    private InputCmdHandler? _previousEscape;

    public MainViewport Viewport => UserInterfaceManager.ActiveScreen!.GetWidget<MainViewport>()!;

    protected override void Startup()
    {
        base.Startup();
        UserInterfaceManager.LoadScreen<MassFabricatorScreen>();
        UserInterfaceManager.GetUIController<ViewportUIController>().ReloadViewport();
        _overlays.AddOverlay(new MassFabricatorOverlay());
        _previousEscape = _input.GetInputCommand(ContentKeyFunctions.EscapeContext);
        _input.SetInputCommand(ContentKeyFunctions.EscapeContext, null);
        _entities.System<MassFabricatorClientSystem>().BeginPlacement();
        _input.Contexts.SetActiveContext("fabricator");
    }

    protected override void Shutdown()
    {
        _singleTilePress = false;
        _menu?.Close();
        _menu = null;
        _supportTooltip?.Orphan();
        _supportTooltip = null;
        _supportTooltipKey = null;
        _entities.System<MassFabricatorClientSystem>().ClearPlacement();
        _entities.System<MassFabricatorEraseSystem>().Clear();
        _entities.System<InputSystem>().SetEntityContextActive();
        if (_input.GetInputCommand(ContentKeyFunctions.EscapeContext) == null)
            _input.SetInputCommand(ContentKeyFunctions.EscapeContext, _previousEscape);
        _overlays.RemoveOverlay<MassFabricatorOverlay>();
        _eyes.MainViewport = UserInterfaceManager.MainViewport;
        UserInterfaceManager.UnloadScreen();
        base.Shutdown();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var client = _entities.System<MassFabricatorClientSystem>();
        UpdateSupportTooltip(client);
        var gun = client.ActiveFabricator;
        if (client.ActiveFabricatorUid is not { } uid ||
            !_entities.TryGetComponent<MaterialStorageComponent>(uid, out var storage))
            return;

        ((MassFabricatorScreen) UserInterfaceManager.ActiveScreen!).UpdateBars(
            gun is { MaxBlueprintLoad: > 0 } ? client.PredictedBlueprintLoad / gun.MaxBlueprintLoad : 0f,
            _entities.System<SharedMaterialStorageSystem>().GetStoredMaterials((uid, storage)), storage.StorageLimit ?? 0);
    }

    private void UpdateSupportTooltip(MassFabricatorClientSystem client)
    {
        if (client.Returning || UserInterfaceManager.CurrentlyHovered != Viewport.Viewport ||
            client.GetPlanDenialTooltipAt(_input.MouseScreenPosition) is not { } denial)
        {
            client.ClearWaveRoute();
            _supportTooltip?.Orphan();
            return;
        }

        _supportTooltip ??= new Tooltip
        {
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        if (_supportTooltipKey != denial.Key)
        {
            _supportTooltipKey = denial.Key;
            var title = Loc.GetString(denial.Blocked ? "mass-fabricator-print-denied" : "mass-fabricator-print-indirect");
            var titleColor = denial.Blocked ? "#ff9b43" : "#ffd47f";
            var text = $"[color={titleColor}][bold]{title}[/bold][/color]";
            if (denial.Key.Length > 0)
                text += $"\n{Loc.GetString(denial.Key)}";
            var backing = new StyleBoxFlat(Color.FromHex("#000007"))
            {
                BorderColor = Color.FromHex(denial.Blocked ? "#784820" : "#6b5832"),
                BorderThickness = new Thickness(2),
            };
            backing.SetContentMarginOverride(StyleBox.Margin.Horizontal, 7);
            _supportTooltip.PanelOverride = backing;
            _supportTooltip.InvalidateMeasure();
            _supportTooltip.SetMessage(FormattedMessage.FromMarkupOrThrow(text));
        }
        if (_supportTooltip.Parent == null)
            UserInterfaceManager.PopupRoot.AddChild(_supportTooltip);
        Tooltips.PositionTooltip(_supportTooltip);
    }

    protected override void OnKeyBindStateChanged(ViewportBoundKeyEventArgs args)
    {
        if (args.KeyEventArgs.Function == EngineKeyFunctions.EditorPlaceObject)
        {
            if (args.KeyEventArgs.State == BoundKeyState.Up && _singleTilePress)
            {
                _singleTilePress = false;
                _entities.System<MassFabricatorClientSystem>().EndPlacementPress();
                args.KeyEventArgs.Handle();
                return;
            }

            if (args.KeyEventArgs.State == BoundKeyState.Down &&
                _placement is PlacementManager
                {
                    IsActive: true,
                    CurrentPermission.IsTile: true,
                    CurrentMode: AlignMassFabricator mode,
                } singlePlacement)
            {
                if (!_singleTilePress)
                {
                    _singleTilePress = true;
                    if (singlePlacement.PlacementType == PlacementManager.PlacementTypes.None)
                    {
                        mode.AlignPlacementMode(args.KeyEventArgs.PointerLocation);
                        singlePlacement.HandlePlacement();
                    }
                    else
                        mode.PlaceDragTiles();
                }

                args.KeyEventArgs.Handle();
                return;
            }
        }

        var erase = _entities.System<MassFabricatorEraseSystem>();
        if (erase.Active && (args.KeyEventArgs.Function == EngineKeyFunctions.EditorPlaceObject ||
                             args.KeyEventArgs.Function == EngineKeyFunctions.EditorGridPlace))
        {
            if (args.KeyEventArgs.State == BoundKeyState.Down)
                erase.Click(args.KeyEventArgs.Function == EngineKeyFunctions.EditorGridPlace);
            args.KeyEventArgs.Handle();
            return;
        }

        if (args.KeyEventArgs.State == BoundKeyState.Up)
        {
            if (args.KeyEventArgs.Function == ContentKeyFunctions.RotateObjectClockwise ||
                args.KeyEventArgs.Function == ContentKeyFunctions.ToggleStanding)
            {
                _rotationKeyHeld = false;
                args.KeyEventArgs.Handle();
                return;
            }
            if (args.KeyEventArgs.Function == EngineKeyFunctions.EditorGridPlace &&
                _placement is PlacementManager { PlacementType: PlacementManager.PlacementTypes.Grid } manager)
            {
                if (manager.CurrentPermission?.IsTile == true && manager.CurrentMode is AlignMassFabricator tiles)
                    tiles.PlaceDragTiles();
                else if (manager.CurrentMode is not AlignMassFabricator alignment ||
                    alignment.HasValidDragCursor(_input.MouseScreenPosition))
                    manager.HandlePlacement();
                else
                    manager.PlacementType = PlacementManager.PlacementTypes.None;
            }
            base.OnKeyBindStateChanged(args);
            if (args.KeyEventArgs.Function == EngineKeyFunctions.EditorPlaceObject ||
                args.KeyEventArgs.Function == EngineKeyFunctions.EditorGridPlace)
                _entities.System<MassFabricatorClientSystem>().EndPlacementPress();
            return;
        }

        var function = args.KeyEventArgs.Function;
        if (function == ContentKeyFunctions.EscapeContext ||
            function == EngineKeyFunctions.EditorPlaceObject ||
            function == EngineKeyFunctions.EditorGridPlace ||
            function == EngineKeyFunctions.EditorRotateObject)
        {
            base.OnKeyBindStateChanged(args);
            return;
        }

        if (function == ContentKeyFunctions.RotateObjectClockwise || function == ContentKeyFunctions.ToggleStanding)
        {
            if (!_rotationKeyHeld && !_entities.System<MassFabricatorClientSystem>().Returning &&
                _entities.System<MassFabricatorClientSystem>().ActiveFabricator?.SelectedBlueprint != null)
            {
                (_placement as PlacementManager)?.Rotate();
                _rotationKeyHeld = true;
            }
            args.KeyEventArgs.Handle();
            return;
        }

        if (function == EngineKeyFunctions.UseSecondary)
        {
            var system = _entities.System<MassFabricatorClientSystem>();
            if (!system.Returning && system.ActiveFabricator is { } fabricator)
            {
                _menu?.Close();
                erase.Clear();
                system.Select(null, null, MassFabricatorEraseMode.None);
                _menu = UserInterfaceManager.CreateWindow<MassFabricatorMenu>();
                _menu.Populate(fabricator);
                _menu.Selected += system.Select;
                _menu.OpenCenteredAt(_input.MouseScreenPosition.Position / _clyde.ScreenSize);
            }
            return;
        }

        if (function != EngineKeyFunctions.MoveUp && function != EngineKeyFunctions.MoveDown &&
            function != EngineKeyFunctions.MoveLeft && function != EngineKeyFunctions.MoveRight &&
            function != EngineKeyFunctions.Walk)
            return;

        if (!_entities.System<MassFabricatorClientSystem>().Returning)
            base.OnKeyBindStateChanged(args);
    }
}

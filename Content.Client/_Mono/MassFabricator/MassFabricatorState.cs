using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Viewport;
using Content.Client.Viewport;
using Content.Shared.Input;
using Content.Shared._Mono.MassFabricator;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.GameObjects;
using Robust.Client.Placement;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

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
        _menu?.Close();
        _menu = null;
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
        var gun = client.ActiveFabricator;
        ((MassFabricatorScreen) UserInterfaceManager.ActiveScreen!).UpdateBars(
            gun is { MaxBlueprintLoad: > 0 } ? client.PredictedBlueprintLoad / gun.MaxBlueprintLoad : 0f,
            gun is { MaxCharge: > 0 } && client.ActiveFabricatorUid is { } uid
                ? (float) _entities.System<MassFabricatorHologramSystem>().PredictedRemainingCharge(uid, gun) / gun.MaxCharge : 0f);
    }

    protected override void OnKeyBindStateChanged(ViewportBoundKeyEventArgs args)
    {
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
                manager.HandlePlacement();
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

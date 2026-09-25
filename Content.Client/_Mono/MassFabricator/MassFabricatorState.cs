using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Viewport;
using Content.Client.Viewport;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class MassFabricatorState : GameplayStateBase, IMainViewportState
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IEyeManager _eyes = default!;
    [Dependency] private IInputManager _input = default!;

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
    }

    protected override void Shutdown()
    {
        if (_input.GetInputCommand(ContentKeyFunctions.EscapeContext) == null)
            _input.SetInputCommand(ContentKeyFunctions.EscapeContext, _previousEscape);
        _overlays.RemoveOverlay<MassFabricatorOverlay>();
        _eyes.MainViewport = UserInterfaceManager.MainViewport;
        UserInterfaceManager.UnloadScreen();
        base.Shutdown();
    }

    protected override void OnKeyBindStateChanged(ViewportBoundKeyEventArgs args)
    {
        if (args.KeyEventArgs.State == BoundKeyState.Up)
        {
            base.OnKeyBindStateChanged(args);
            return;
        }

        var function = args.KeyEventArgs.Function;
        if (function == ContentKeyFunctions.EscapeContext)
        {
            base.OnKeyBindStateChanged(args);
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

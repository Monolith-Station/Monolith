using Content.Client.Gameplay;
using Content.Client.Sandbox;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Forge.WallPaint.UI;

public sealed partial class WallPaintUIController : UIController, IOnStateExited<GameplayState>, IOnSystemChanged<SandboxSystem>
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    [UISystemDependency] private readonly SandboxSystem _sandbox = default!;

    private WallPaintWindow? _window;

    public void ToggleWindow()
    {
        EnsureWindow();

        if (_window!.IsOpen)
            _window.Close();
        else if (_sandbox.SandboxAllowed)
            _window.Open();
    }

    public void OnStateExited(GameplayState state)
    {
        DeactivatePaint();

        if (_window == null)
            return;

        _window.Dispose();
        _window = null;
    }

    public void OnSystemLoaded(SandboxSystem system)
    {
        _sandbox.SandboxDisabled += CloseWindow;
    }

    public void OnSystemUnloaded(SandboxSystem system)
    {
        _sandbox.SandboxDisabled -= CloseWindow;
        DeactivatePaint();
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        DeactivatePaint();
        _window = UIManager.CreateWindow<WallPaintWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterLeft);
    }

    public void CloseWindow()
    {
        if (_window == null || _window.Disposed)
            return;

        _window.Close();
    }

    private void DeactivatePaint()
    {
        if (_systems.TryGetEntitySystem<WallPaintPlacementSystem>(out var wallPaint))
            wallPaint.Deactivate();
    }
}

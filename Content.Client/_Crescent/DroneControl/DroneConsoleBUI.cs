using Content.Shared._Crescent.DroneControl;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Client._Crescent.DroneControl;

[UsedImplicitly]
public sealed class DroneConsoleBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    private TransformSystem _xform;
    private DroneConsoleWindow? _window;

    public DroneConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _xform = _entMan.System<TransformSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = new DroneConsoleWindow();
        _window.OnClose += Close;
        _window.OpenCentered();

        _window.OnRadarOrder += OnRadarClick;
    }

    private void OnRadarClick(EntityCoordinates coord)
    {
        if (_window == null) return;

        var selected = _window.GetSelectedDrones();
        if (selected.Count == 0) return;

        var worldPos = _xform.ToMapCoordinates(coord).Position;

        // Perform Grid Detection on Client
        var xform = _entMan.GetComponent<TransformComponent>(Owner);
        var mapId = xform.MapID;

        // Create a small box around the click
        var box = Box2.FromDimensions(worldPos, new Vector2(0.5f, 0.5f));

        EntityUid? foundGrid = null;

        _mapManager.FindGridsIntersecting(mapId, box, (EntityUid uid, MapGridComponent comp) =>
        {
            foundGrid = uid;
            return false; // Stop at first grid found
        }, true, false);

        if (foundGrid != null)
            SendMessage(new DroneConsoleTargetMessage(selected, _entMan.GetNetEntity(foundGrid.Value)));
        else
            SendMessage(new DroneConsoleMoveMessage(selected, worldPos));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DroneConsoleBoundUserInterfaceState cast)
        {
            _window?.UpdateState(cast);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}

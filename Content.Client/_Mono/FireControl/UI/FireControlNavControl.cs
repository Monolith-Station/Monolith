using System.Linq;
using System.Numerics;
using Content.Client.Shuttles.UI;
using Content.Shared._Mono.FireControl;
using Content.Shared.Physics;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Client._Mono.Radar;
using Content.Shared._Mono.Detection;
using Content.Shared._Mono.Radar;
using Content.Shared._Crescent.ShipShields;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._Mono.FireControl.UI;

public sealed class FireControlNavControl : ShuttleNavControl
{
    private readonly SharedTransformSystem _transform;
    private readonly SharedPhysicsSystem _physics;
    private readonly RadarBlipsSystem _blips;

    private EntityUid? _activeConsole;
    private FireControllableEntry[]? _controllables;
    private HashSet<NetEntity> _selectedWeapons = new();

    // Add a limit to how often we update the cursor position to prevent network spam
    private float _lastCursorUpdateTime = 0f;
    private const float CursorUpdateInterval = 0.1f; // 10 updates per second

    public FireControlNavControl() : base(64f, 512f, 512f)
    {
        IoCManager.InjectDependencies(this);
        _blips = EntManager.System<RadarBlipsSystem>();
        _physics = EntManager.System<SharedPhysicsSystem>();
        _transform = EntManager.System<SharedTransformSystem>();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_isMouseInside)
            // Continuously update the cursor position for guided missiles
            TryUpdateCursorPosition(_lastMousePos);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_coordinates == null || _rotation == null)
            return;

        var xformQuery = EntManager.GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(_coordinates.Value.EntityId, out var xform)
            || xform.MapID == MapId.Nullspace)
        {
            return;
        }

        base.Draw(handle);

        var mapPos = _transform.ToMapCoordinates(_coordinates.Value);
        var posMatrix = Matrix3Helpers.CreateTransform(_coordinates.Value.Position, _rotation.Value);
        var ourEntRot = RotateWithEntity ? _transform.GetWorldRotation(xform) : _rotation.Value;
        var ourEntMatrix = Matrix3Helpers.CreateTransform(_transform.GetWorldPosition(xform), ourEntRot);
        var shuttleToWorld = Matrix3x2.Multiply(posMatrix, ourEntMatrix);
        Matrix3x2.Invert(shuttleToWorld, out var worldToShuttle);
        var shuttleToView = Matrix3x2.CreateScale(new Vector2(MinimapScale, -MinimapScale)) * Matrix3x2.CreateTranslation(MidPointVector);
        var worldToView = worldToShuttle * shuttleToView;
        Matrix3x2.Invert(worldToView, out var viewToWorld);

        var blips = _blips.GetCurrentBlips();
        var colors = new Dictionary<NetEntity, Color>();
        foreach (var blip in blips)
            colors[blip.NetUid] = blip.Color;

        if (_controllables != null)
        {
            foreach (var controllable in _controllables)
            {
                var coords = EntManager.GetCoordinates(controllable.Coordinates);
                var worldPos = _transform.ToMapCoordinates(coords).Position;

                if (_selectedWeapons.Contains(controllable.NetEntity))
                {
                    var cursorViewPos = InverseScalePosition(_lastMousePos);
                    cursorViewPos = ScalePosition(cursorViewPos);

                    var cursorWorldPos = Vector2.Transform(cursorViewPos, viewToWorld);

                    var direction = cursorWorldPos - worldPos;
                    var ray = new CollisionRay(worldPos, direction.Normalized(), (int)CollisionGroup.Impassable);

                    var results = _physics.IntersectRay(xform.MapID, ray, direction.Length(), ignoredEnt: _coordinates?.EntityId);

                    if (!results.Any() && colors.TryGetValue(controllable.NetEntity, out var color))
                        handle.DrawLine(Vector2.Transform(worldPos, worldToView), cursorViewPos, color.WithAlpha(0.3f));
                }
            }
        }
    }

    public void UpdateControllables(EntityUid console, FireControllableEntry[] controllables)
    {
        _activeConsole = console;
        _controllables = controllables;
    }

    public void UpdateSelectedWeapons(HashSet<NetEntity> selectedWeapons)
    {
        _selectedWeapons = selectedWeapons;
    }

    private void TryUpdateCursorPosition(Vector2 relativePosition)
    {
        var currentTime = IoCManager.Resolve<IGameTiming>().CurTime.TotalSeconds;
        if (currentTime - _lastCursorUpdateTime < CursorUpdateInterval)
            return;

        _lastCursorUpdateTime = (float)currentTime;

        // Convert mouse position to world coordinates for missile tracking
        if (_coordinates == null || _rotation == null || OnRadarClick == null)
            return;

        var a = InverseScalePosition(relativePosition);
        var relativeWorldPos = new Vector2(a.X, -a.Y);
        relativeWorldPos = _rotation.Value.RotateVec(relativeWorldPos);
        var coords = _coordinates.Value.Offset(relativeWorldPos);

        // This will update the server of our cursor position without triggering actual firing
        OnRadarClick?.Invoke(coords);
    }

    /// <summary>
    /// Returns true if the mouse button is currently pressed down
    /// </summary>
    public bool IsMouseDown() => _isMouseDown;
}

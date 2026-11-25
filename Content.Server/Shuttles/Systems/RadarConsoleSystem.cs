using System.Numerics;
using Content.Server._Mono.Sensors;
using Content.Server.UserInterface;
using Content.Shared._Mono.Sensors.Events;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.PowerCell;
using Content.Shared.Movement.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Shared.PowerCell;
using Content.Shared.Movement.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class RadarConsoleSystem : SharedRadarConsoleSystem
{
    [Dependency] private SharedTransformSystem _transform = default!; // Mono
    [Dependency] private ShuttleConsoleSystem _console = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private ShipSensorsSystem _sensors = default!; // Mono

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadarConsoleComponent, ComponentStartup>(OnRadarStartup);
        SubscribeLocalEvent<RadarConsoleComponent, BoundUIOpenedEvent>(OnUIOpened); // Frontier
        SubscribeLocalEvent<RadarConsoleComponent, SensorsConsoleRefreshEvent>(OnSensorsConsoleRefresh); // Mono
    }

    private void OnRadarStartup(EntityUid uid, RadarConsoleComponent component, ComponentStartup args)
    {
        UpdateState(uid, component);
    }

    // Frontier
    private void OnUIOpened(EntityUid uid, RadarConsoleComponent component, ref BoundUIOpenedEvent args)
    {
        UpdateState(uid, component);
    }
    // End Frontier

    // Mono
    private void OnSensorsConsoleRefresh(EntityUid uid, RadarConsoleComponent component, SensorsConsoleRefreshEvent args)
    {
        UpdateState(uid, component);
    }

    protected override void UpdateState(EntityUid uid, RadarConsoleComponent component)
    {
        var xform = Transform(uid);
        // Mono
        var parentUid = xform.GridUid;
        EntityCoordinates? coordinates = null;
        Angle? angle = null;
        if (component.FollowEntity)
        {
            coordinates = new EntityCoordinates(uid, Vector2.Zero);
            angle = Angle.Zero; // Frontier: Angle.Zero<Angle.FromDegrees(180) // Mono - frontier strikes again
        }
        else if (parentUid is { } parent)
        {
            coordinates = _transform.WithEntityId(xform.Coordinates, parent);
            angle = _transform.GetWorldRotation(xform) - _transform.GetWorldRotation(parent);
        }

        if (_uiSystem.HasUi(uid, RadarConsoleUiKey.Key))
        {
            NavInterfaceState state;
            var docks = _console.GetAllDocks();

            if (coordinates != null && angle != null)
            {
                state = _console.GetNavState(uid, docks, coordinates.Value, angle.Value);
            }
            else
            {
                state = _console.GetNavState(uid, docks);
            }

            // Frontier: ghost radar restrictions
            if (component.MaxIffRange != null)
                state.MaxIffRange = component.MaxIffRange.Value;
            state.HideCoords = component.HideCoords;
            // End Frontier

            var sensorsState = _sensors.BuildBuiState(uid); // Mono
            _uiSystem.SetUiState(uid, RadarConsoleUiKey.Key,
                new NavBoundUserInterfaceState(state, sensorsState)); // Mono
        }
    }
}

using System.Numerics;
using Content.Server.UserInterface;
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

public sealed class RadarConsoleSystem : SharedRadarConsoleSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!; // Mono
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadarConsoleComponent, ComponentStartup>(OnRadarStartup);
        SubscribeLocalEvent<RadarConsoleComponent, BoundUIOpenedEvent>(OnUIOpened); // Frontier
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

            _uiSystem.SetUiState(uid, RadarConsoleUiKey.Key, new NavBoundUserInterfaceState(state));
        }
    }
}

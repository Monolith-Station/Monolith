using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._FarHorizons.StarSystem;

public abstract partial class SharedPlanetTransitSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    protected static readonly TimeSpan DescentChargeTime = TimeSpan.FromSeconds(3);
    protected static readonly TimeSpan DriveRespoolTime = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan DischargeStunTime = TimeSpan.FromSeconds(5);

    protected void BeginPredictedDescent(EntityUid console, PlanetDescentRequestMessage args)
    {
        if (Transform(console).GridUid is not { } grid || !HasComp<MapGridComponent>(grid))
            return;

        var planetUid = GetEntity(args.Planet);
        if (!TryComp<PlanetBodyComponent>(planetUid, out var planet))
            return;

        if (planet.SurfaceNetwork is not { } surface)
        {
            _popup.PopupPredicted(Loc.GetString("planet-descent-no-surface"), console, args.Actor);
            return;
        }

        var availability = GetDescentAvailability(grid, planetUid, planet, surface);
        if (availability != DescentAvailability.Available)
        {
            var message = availability switch
            {
                DescentAvailability.TooFar => "planet-descent-too-far",
                DescentAvailability.Respooling => "planet-descent-respooling",
                _ => "planet-descent-unavailable",
            };
            _popup.PopupPredicted(Loc.GetString(message), console, args.Actor);
            return;
        }

        var transit = AddComp<PlanetTransitComponent>(grid);
        transit.Planet = planetUid;
        transit.Direction = PlanetTransitDirection.Descent;
        transit.Phase = PlanetTransitPhase.Charging;
        transit.PhaseStart = _timing.CurTime;
        transit.PhaseEnd = _timing.CurTime + DescentChargeTime;
        transit.Grids.Add(grid);
        Dirty(grid, transit);

        var pilotLockAdded = !HasComp<PreventPilotComponent>(grid);
        EnsureComp<PreventPilotComponent>(grid);
        PredictedDescentStarted(grid, transit, pilotLockAdded);
    }

    protected DescentAvailability GetDescentAvailability(EntityUid grid,
        EntityUid planetUid,
        PlanetBodyComponent planet,
        EntityUid surface,
        bool activeTransit = false)
    {
        if (HasComp<PlanetTransitFailureComponent>(grid))
            return DescentAvailability.Respooling;

        if ((!activeTransit && HasComp<PlanetTransitComponent>(grid)) ||
            !HasComp<CEZMapNetworkComponent>(surface) ||
            Transform(grid).MapUid is not { } map ||
            map != Transform(planetUid).MapUid ||
            HasComp<CEZMapComponent>(map) ||
            HasComp<CEZTransitMapComponent>(map))
        {
            return DescentAvailability.Unavailable;
        }

        var radius = planet.ApproachRadius;
        return (_transform.GetWorldPosition(grid) - _transform.GetWorldPosition(planetUid)).LengthSquared() <= radius * radius
            ? DescentAvailability.Available
            : DescentAvailability.TooFar;
    }

    protected virtual void PredictedDescentStarted(EntityUid grid,
        PlanetTransitComponent transit,
        bool pilotLockAdded)
    {
    }

    protected enum DescentAvailability : byte
    {
        Available,
        TooFar,
        Respooling,
        Unavailable,
    }
}

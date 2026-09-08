using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.Planets.Shields;

namespace Content.Server._CE.Planets.Shields;

public sealed partial class CEPlanetShieldSystem : EntitySystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;

    public bool TryGetPlanetForMap(EntityUid map, out EntityUid planet)
    {
        planet = default;
        if (!_zLevels.TryGetMapNetwork(map, out var network) ||
            !TryComp<PlanetSurfaceComponent>(network.Owner, out var surface) ||
            !HasComp<PlanetBodyComponent>(surface.Planet))
            return false;

        planet = surface.Planet;
        return true;
    }

    public bool SetShieldActive(EntityUid planet, bool active)
    {
        if (!HasComp<PlanetBodyComponent>(planet))
            return false;

        var shield = EnsureComp<CEPlanetShieldComponent>(planet);
        if (shield.Active == active)
            return true;

        shield.Active = active;
        Dirty(planet, shield);
        return true;
    }

    public bool IsShielded(EntityUid planet)
    {
        return TryComp<CEPlanetShieldComponent>(planet, out var shield) && shield.Active;
    }
}

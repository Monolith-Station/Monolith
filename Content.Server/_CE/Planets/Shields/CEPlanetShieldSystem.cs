using System.Numerics;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.Planets.Shields;
using Robust.Shared.Timing;

namespace Content.Server._CE.Planets.Shields;

public sealed partial class CEPlanetShieldSystem : EntitySystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private IGameTiming _timing = default!;

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

        var now = _timing.CurTime;
        shield.TransitionFrom = shield.PhaseAt(now);
        shield.TransitionAt = now;
        shield.Active = active;
        if (!active)
            shield.Impacts.Clear();
        Dirty(planet, shield);
        return true;
    }

    public void RecordImpact(EntityUid planet, Vector2 entry, TimeSpan time, float strength)
    {
        if (!TryComp<CEPlanetShieldComponent>(planet, out var shield) || !shield.Active)
            return;

        var now = _timing.CurTime;
        shield.Impacts.RemoveAll(impact => (now - impact.Time).TotalSeconds >= CEPlanetShieldComponent.ImpactLifetime);
        if (shield.Impacts.Count >= CEPlanetShieldComponent.MaxImpacts)
            shield.Impacts.RemoveAt(0);
        shield.Impacts.Add(new(entry, time, strength));
        Dirty(planet, shield);
    }

    public bool IsShielded(EntityUid planet)
    {
        return TryComp<CEPlanetShieldComponent>(planet, out var shield) && shield.Active;
    }
}

using System.Linq;
using Content.Shared._FarHorizons.StarSystem.Helpers;

namespace Content.Shared._FarHorizons.StarSystem;

public abstract partial class SharedStarSystemMapSystem : EntitySystem
{
    public List<Planet> GetPrettyPlanets(Entity<StarSystemMapComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp) ||
            ent.Comp.StarSystem == null)
            return new List<Planet>();

        return ent.Comp.StarSystem.Planets.OrderByDescending(p => p.GetPettiness()).ToList();
    }
}

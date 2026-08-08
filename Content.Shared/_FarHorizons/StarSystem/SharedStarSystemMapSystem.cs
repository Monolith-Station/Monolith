using System.Linq;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem;

public abstract partial class SharedStarSystemMapSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoMan = default!;

    public List<Planet> GetPrettyPlanets(Entity<StarSystemMapComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp) ||
            ent.Comp.StarSystem == null)
            return new List<Planet>();
        
        return ent.Comp.StarSystem.Planets.OrderByDescending(p => p.GetPettiness()).ToList();
    }
}
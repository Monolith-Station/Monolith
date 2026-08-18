using System.Numerics;

namespace Content.Shared._FarHorizons.StarSystem.Helpers;

[DataDefinition]
public sealed partial class PlanetarySystem
{
    [ViewVariables] public Star Star;
    [ViewVariables] public List<Planet> Planets;
    [ViewVariables] public AsteroidBelt? AsteroidBelt;

    public PlanetarySystem(Star star, List<Planet> planets, AsteroidBelt? asteroidBelt)
    {
        Star = star;
        Planets = planets;
        AsteroidBelt = asteroidBelt;
    }
}
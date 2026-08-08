using System.Linq;
using System.Numerics;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._FarHorizons.StarSystem;

public abstract partial class SharedStarSystemMapSystem
{
    public const float MIN_STAR_PADDING = 500f;
    public const float BASE_ORBIT_DISTANCE = 1000f;
    public const float ORBIT_SPACING_FACTOR = 1.4f;
    public const float ORBIT_SECTOR_ARC = MathF.PI / 6f; // 30 degrees
    public const float ORBIT_JITTER_PERCENT = 0.5f;

    public PlanetarySystem? MakePlanetarySystem(Entity<StarSystemMapComponent> ent)
    {
        if (ent.Comp.Seed == null) return null;
        var rand = new System.Random(ent.Comp.Seed.Value);

        var stars = _protoMan.EnumeratePrototypes<StarTypePrototype>().OrderBy(p => p.ID).ToList();
        var pickedStar = rand.Pick(stars);

        var solarMass = rand.NextFloat(pickedStar.SolarMass.Min, pickedStar.SolarMass.Max);
        var star = new Star(solarMass, pickedStar.Color, pickedStar.Shader);
        star.GenerateName(rand);

        var orbitOffset = new Vector2(rand.NextFloat(), rand.NextFloat());

        AsteroidBelt? asteroidBelt = null;

        var planets = ResolvePlanets(rand, star, pickedStar, orbitOffset, ref asteroidBelt);

        return new PlanetarySystem(star, planets, asteroidBelt);
    }

    private List<Planet> ResolvePlanets(System.Random rand, Star star, StarTypePrototype starProto, Vector2 orbitOffset, ref AsteroidBelt? belt)
    {
        var output = new List<Planet>();

        var sectorAngle = rand.NextFloat(0f, MathF.Tau);

        var systemCenterOffset = orbitOffset * star.Radius * Star.NAV_PIXEL_SIZE * 0.5f;

        var slotId = 0;
        var nameId = 0;
        foreach (var orbit in starProto.Orbits)
        {
            if (orbit.Prob < 1f && rand.NextFloat() >= orbit.Prob)
                continue;

            slotId++;
            
            if (orbit.Type == OrbitType.Belt &&
                starProto.AsteroidBelts.Any())
            {
                var currentDist = SlotDistance(slotId, star, 100f);
                var prevDist = SlotDistance(slotId - 1, star, 100f);
                var nextDist = SlotDistance(slotId + 1, star, 100f);

                var innerHalf = (currentDist - prevDist) * 0.5f * 0.75f;
                var outerHalf = (nextDist - currentDist) * 0.5f * 0.75f;

                var innerRadius = currentDist - innerHalf;
                var outerRadius = currentDist + outerHalf;

                var radialSize = new Vector2(innerRadius, outerRadius);

                if (belt == null)
                {
                    var beltProto = _protoMan.Index(rand.Pick(starProto.AsteroidBelts));
                    var beltPalette = rand.Pick(beltProto.Palettes);
                    belt = new AsteroidBelt(systemCenterOffset, radialSize, beltProto.Shader, beltPalette);
                }
                else
                    belt.Expand(radialSize);
            }

            var planets = _protoMan.EnumeratePrototypes<PlanetTypePrototype>()
                .Where(p => p.Orbit.Contains(orbit.Type))
                .OrderBy(p => p.ID)
                .ToList();

            if (!planets.Any()) continue;

            var planet = rand.Pick(planets);

            var palettes = planet.Palettes;
            if (!palettes.Any()) continue;

            var palette = rand.Pick(palettes);

            var earthMass = planet.EarthMass.RollValue(rand);
            var rotation = rand.NextFloat(0f, MathF.Tau);

            var hasAtmosphere = rand.NextFloat() < planet.AtmosphereProbability;
            PlanetaryAtmosphere? atmosphere = null;
            if (hasAtmosphere && planet.Atmospheres.Any())
            {
                var atmosProto = rand.Pick(planet.Atmospheres);
                atmosphere = new PlanetaryAtmosphere(rand, _protoMan, atmosProto);
            }

            var hasLiquid = rand.NextFloat() < planet.LiquidProbability;
            PlanetaryLiquid? liquid = null;
            if (hasLiquid && planet.Liquids.Any())
            {
                var liquidProto = rand.Pick(planet.Liquids);
                liquid = new PlanetaryLiquid(rand, _protoMan, liquidProto);
            }

            PlanetaryRings? rings = null;
            if (planet.Rings.Any() && planet.RingProbability > 0f && rand.NextFloat() < planet.RingProbability)
            {
                var ringType = rand.Pick(planet.Rings);
                rings = new PlanetaryRings(rand, _protoMan, ringType);
            }

            var position = ResolvePlanetPosition(rand, slotId, star, earthMass, sectorAngle, systemCenterOffset);
            
            var hueShift = rand.NextFloat();
            var saturationShift = rand.NextFloat();

            var customData = new PlanetCustomValues(rand, planet);

            output.Add(new Planet(
                position,
                star.GetPlanetName(nameId),
                earthMass, 
                rotation, 
                atmosphere, 
                liquid, 
                palette, 
                planet.Shader, 
                hueShift, 
                saturationShift, 
                customData, 
                rings,
                planet.BasePrettiness
            ));
            
            nameId++;
        }

        return output;
    }

    private static float SlotDistance(int slotId, Star star, float mass)
    {
        var spacing = MathF.Pow(ORBIT_SPACING_FACTOR, slotId);

        var starRadiusPx = star.Radius * Star.NAV_PIXEL_SIZE;
        var planetRadiusPx = Planet.GetRadius(mass) * Planet.NAV_PIXEL_SIZE;

        var minClearance = starRadiusPx + planetRadiusPx + MIN_STAR_PADDING;

        return minClearance + (BASE_ORBIT_DISTANCE * spacing);
    }

    private static Vector2 ResolvePlanetPosition(System.Random rand, int slotId, Star star, float mass, float sectorAngle, Vector2 systemOffset)
    {
        var baseDistance = SlotDistance(slotId, star, mass);

        var planetRadiusPx = Planet.GetRadius(mass) * Planet.NAV_PIXEL_SIZE;
        var maxJitter = planetRadiusPx * ORBIT_JITTER_PERCENT;
        var distanceVariation = rand.NextFloat(-maxJitter, maxJitter);

        var starRadiusPx = star.Radius * Star.NAV_PIXEL_SIZE;
        var minSafeDistance = starRadiusPx + planetRadiusPx + MIN_STAR_PADDING;
        var actualDistance = MathF.Max(minSafeDistance, baseDistance + distanceVariation);

        var minAngle = sectorAngle - (ORBIT_SECTOR_ARC / 2f);
        var maxAngle = sectorAngle + (ORBIT_SECTOR_ARC / 2f);
        var angle = rand.NextFloat(minAngle, maxAngle);

        var x = actualDistance * MathF.Cos(angle);
        var y = actualDistance * MathF.Sin(angle);

        return new Vector2(x, y) + systemOffset;
    }
}
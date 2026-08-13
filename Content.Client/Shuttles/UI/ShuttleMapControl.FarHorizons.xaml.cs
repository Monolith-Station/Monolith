using System.Numerics;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleMapControl
{
    private void DrawStarSystem(DrawingHandleScreen handle, Matrix3x2 matty)
    {
        if (!EntManager.TryGetComponent<TransformComponent>(_shuttleEntity, out var shuttleTransform) ||
            shuttleTransform.MapUid == null ||
            !EntManager.TryGetComponent<StarSystemMapComponent>(shuttleTransform.MapUid.Value, out var starSystem) ||
            starSystem.StarSystem == null)
            return;
        
        var starPos = Vector2.Transform(starSystem.StarSystem.Star.Position, matty);
        starPos = starPos with { Y = -starPos.Y };
        starPos = ScalePosition(starPos);
        var starRadius = Star.MAP_PIXEL_SIZE * starSystem.StarSystem.Star.Radius * MinimapScale;

        handle.DrawCircle(starPos, starRadius, starSystem.StarSystem.Star.Color);

        foreach (var planet in starSystem.StarSystem.Planets)
        {
            var planetPos = Vector2.Transform(planet.Position, matty);
            planetPos = planetPos with { Y = -planetPos.Y };
            planetPos = ScalePosition(planetPos);
            var planetRadius = Planet.MAP_PIXEL_SIZE * planet.Radius * MinimapScale;
            handle.DrawCircle(planetPos, planetRadius, Color.Gray);
        }
    }
}
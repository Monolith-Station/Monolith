using System.Numerics;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    private void DrawStarSystem(DrawingHandleScreen handle, Matrix3x2 worldToShuttle, Matrix3x2 shuttleToView, EntityUid? mapUid)
    {
        if (!EntManager.TryGetComponent<StarSystemMapComponent>(mapUid, out var starSystem) ||
            starSystem.StarSystem == null)
            return;

        var worldToView = worldToShuttle * shuttleToView;
        var viewScale = MathF.Sqrt((worldToView.M11 * worldToView.M11) + (worldToView.M12 * worldToView.M12));

        var starPos = Vector2.Transform(starSystem.StarSystem.Star.Position + starSystem.StarOffset, worldToView);
        var starRadius = Star.NAV_PIXEL_SIZE * starSystem.StarSystem.Star.Radius * viewScale;

        handle.DrawCircle(starPos, starRadius, starSystem.StarSystem.Star.Color.WithAlpha(0.5f));

        foreach (var planet in starSystem.StarSystem.Planets)
        {
            var planetPos = Vector2.Transform(planet.Position + starSystem.StarOffset, worldToView);
            var planetRadius = Planet.NAV_PIXEL_SIZE * planet.Radius * viewScale;
            handle.DrawCircle(planetPos, planetRadius, Color.Gray.WithAlpha(0.5f));
        }
    }
}

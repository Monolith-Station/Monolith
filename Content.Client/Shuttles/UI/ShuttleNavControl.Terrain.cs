using System.Numerics;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    public bool ShowTerrain { get; set; } = true;

    private float GetRadarAltitude(EntityUid uid)
    {
        var observer = _consoleEntity ?? _coordinates?.EntityId;
        if (observer == null)
            return 0f;

        var levels = EntManager.System<CESharedZLevelsSystem>();
        return levels.GetAbsoluteAltitude(uid) - levels.GetAbsoluteAltitude(observer.Value);
    }

    private float? GetStackRadarAltitude(EntityUid uid)
    {
        var map = EntManager.GetComponent<TransformComponent>(uid).MapUid;
        return _detection.GetRadarNetwork(map) != null
            ? EntManager.System<CESharedZLevelsSystem>().GetAbsoluteAltitude(uid)
            : null;
    }

    protected void DrawTerrain(DrawingHandleScreen handle, Matrix3x2 worldToView, Vector2 center, EntityUid? map)
    {
        if (ShowTerrain && FindTerrainSurface(map, _consoleEntity ?? _coordinates?.EntityId) is { } terrain)
            DrawTerrainPreview(handle, worldToView, terrain, _consoleEntity);
    }
}

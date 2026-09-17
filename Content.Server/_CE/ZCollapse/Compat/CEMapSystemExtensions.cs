using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Robust.Shared.Map;

public static class CEMapSystemExtensions
{
    public static IEnumerable<Entity<MapGridComponent>> GetAllGrids(this SharedMapSystem _, MapId mapId)
        => IoCManager.Resolve<IMapManager>().GetAllGrids(mapId);
}

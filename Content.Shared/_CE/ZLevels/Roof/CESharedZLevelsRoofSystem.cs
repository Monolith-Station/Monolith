/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Shared._CE.ZLevels.Roof;

public abstract partial class CESharedZLevelsRoofSystem : EntitySystem
{
    [Dependency] protected ITileDefinitionManager TilDefMan = default!;

    public bool IsWeatherBlocked(EntityUid map, Vector2i index) =>
        TryComp<CEZLevelRoofComponent>(map, out var roof) && IsCovered(roof.WeatherData, index);

    protected bool CastsShadow(Tile tile) =>
        !tile.IsEmpty && !((ContentTileDefinition) TilDefMan[tile.TypeId]).Transparent;

    protected bool BlocksWeather(Tile tile) =>
        !tile.IsEmpty && ((ContentTileDefinition) TilDefMan[tile.TypeId]).BlocksWeather;

    protected static bool IsCovered(Dictionary<Vector2i, ulong> data, Vector2i index)
    {
        var chunk = SharedMapSystem.GetChunkIndices(index, RoofComponent.ChunkSize);
        return (data.GetValueOrDefault(chunk) & GetBitFlag(index)) != 0;
    }

    protected static bool SetCover(Dictionary<Vector2i, ulong> data, Vector2i index, bool covered)
    {
        var chunk = SharedMapSystem.GetChunkIndices(index, RoofComponent.ChunkSize);
        var old = data.GetValueOrDefault(chunk);
        var bit = GetBitFlag(index);
        var next = covered ? old | bit : old & ~bit;
        if (old == next)
            return false;

        if (next == 0)
            data.Remove(chunk);
        else
            data[chunk] = next;

        return true;
    }

    private static ulong GetBitFlag(Vector2i index)
    {
        var relative = SharedMapSystem.GetChunkRelative(index, RoofComponent.ChunkSize);
        return 1UL << (relative.X + relative.Y * RoofComponent.ChunkSize);
    }
}

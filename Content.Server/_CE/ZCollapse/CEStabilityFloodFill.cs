/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Map;

namespace Content.Server._CE.ZCollapse;

public static class CEStabilityFloodFill
{
    private static readonly Vector2i[] CardinalOffsets =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    public static void SeedCores(
        Dictionary<(EntityUid, Vector2i), int> stability,
        Queue<((EntityUid Grid, Vector2i Tile) Node, int Value)> queue,
        HashSet<(EntityUid Grid, Vector2i Tile)> liveNodes,
        List<(EntityUid Grid, Vector2i Tile, int Value)> coreSeeds)
    {
        foreach (var (grid, tile, value) in coreSeeds)
            Seed(stability, queue, liveNodes, (grid, tile), value);
    }

    public static int Process(
        Queue<((EntityUid Grid, Vector2i Tile) Node, int Value)> queue,
        Dictionary<(EntityUid, Vector2i), int> stability,
        HashSet<(EntityUid, Vector2i)> liveNodes,
        Dictionary<(EntityUid, Vector2i), List<((EntityUid Grid, Vector2i Tile) Node, int Strength, int Loss)>> bridges,
        int? budget = null)
    {
        var processed = 0;
        while (queue.TryDequeue(out var entry))
        {
            var (node, value) = entry;
            foreach (var offset in CardinalOffsets)
                Seed(stability, queue, liveNodes, (node.Grid, node.Tile + offset), value - 1);

            if (bridges.TryGetValue(node, out var partners))
            {
                foreach (var (partner, strength, loss) in partners)
                    Seed(stability, queue, liveNodes, partner, Math.Min(value - loss, strength));
            }

            if (budget.HasValue && ++processed >= budget.Value)
                break;
        }

        return queue.Count;
    }

    private static void Seed(
        Dictionary<(EntityUid, Vector2i), int> stability,
        Queue<((EntityUid, Vector2i), int)> queue,
        HashSet<(EntityUid, Vector2i)> liveNodes,
        (EntityUid Grid, Vector2i Tile) node,
        int value)
    {
        if (value <= 0 || !liveNodes.Contains(node))
            return;

        if (value > stability.GetValueOrDefault(node, 0))
        {
            stability[node] = value;
            queue.Enqueue((node, value));
        }
    }
}

/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.CPUJob.JobQueues;

namespace Content.Server._CE.ZCollapse;

public sealed class StabilityJob(
    double maxTime,
    HashSet<(EntityUid Grid, Vector2i Tile)> liveNodes,
    List<(EntityUid Grid, Vector2i Tile, int Value)> coreSeeds,
    Dictionary<(EntityUid, Vector2i), List<((EntityUid Grid, Vector2i Tile) Node, int Strength, int Loss)>> bridges,
    CancellationToken cancellation = default)
    : Job<Dictionary<(EntityUid Grid, Vector2i Tile), int>>(maxTime, cancellation)
{
    private const int BatchSize = 256;
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _liveNodes = liveNodes;

    public IReadOnlySet<(EntityUid Grid, Vector2i Tile)> LiveNodes => _liveNodes;

    protected override async Task<Dictionary<(EntityUid Grid, Vector2i Tile), int>?> Process()
    {
        var stability = new Dictionary<(EntityUid, Vector2i), int>();
        var queue = new Queue<((EntityUid Grid, Vector2i Tile) Node, int Value)>();
        CEStabilityFloodFill.SeedCores(stability, queue, _liveNodes, coreSeeds);

        while (CEStabilityFloodFill.Process(queue, stability, _liveNodes, bridges, BatchSize) > 0)
            await SuspendIfOutOfTime();

        return stability;
    }
}

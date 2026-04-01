using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Mono.ShipRepair.Components;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.ShipRepair;
public sealed partial class ShipRepairSystem
{
    [Dependency] private readonly IConsoleHost _conHost = default!;

    public void InitCommands()
    {
        _conHost.RegisterCommand("repairgrid", "Repair a grid to snapshot", "repairgrid <uid>",
            RepairGridCmd);
        _conHost.RegisterCommand("snapshotgrid", "Snapshot a grid's current data for repair", "snapshotgrid <uid>",
            SnapshotGridCmd);
    }

    [AdminCommand(AdminFlags.Admin)]
    public void RepairGridCmd(IConsoleShell shell, string argstr, string[] args)
    {
        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteError("Couldn't parse entity.");
            return;
        }

        if (!TryComp<MapGridComponent>(uid, out var grid))
        {
            shell.WriteError("Entity is not a grid.");
            return;
        }

        if (!TryComp<ShipRepairDataComponent>(uid, out var data))
        {
            shell.WriteError("Entity does not have a repair snapshot.");
            return;
        }

        var tileSet = new List<(Vector2i, Tile)>();
        foreach (var (chunkPos, chunk) in data.Chunks)
        {
            for (var x = 0; x < data.ChunkSize; x++)
            {
                for (var y = 0; y < data.ChunkSize; y++)
                {
                    var idx = x + y * data.ChunkSize;
                    var tileToPlace = chunk.Tiles[idx];
                    if (tileToPlace != Tile.Empty.TypeId)
                        tileSet.Add((chunkPos * data.ChunkSize + new Vector2i(x, y), new Tile(tileToPlace)));
                }
            }
        }
        _map.SetTiles(uid, grid, tileSet);

        foreach (var (chunkPos, chunk) in data.Chunks)
        {
            foreach (var (_, spec) in chunk.Entities)
            {
                var coords = new EntityCoordinates(uid, spec.LocalPosition);

                var origUid = spec.OriginalEntity == null ? (EntityUid?)null : GetEntity(spec.OriginalEntity.Value);
                if (origUid != null && !TerminatingOrDeleted(origUid.Value))
                {
                    var origXform = Transform(origUid.Value);
                    // if it's not on another grid just teleport it
                    if (origXform.Coordinates.TryDistance(EntityManager, coords, out var distance)
                        && distance > 0.01f
                    )
                        // delete it before making replacement, will troll anyone who stole it but this is an admin command and we do not care
                        QueueDel(origUid);
                    else
                        continue; // it's already real and in-place so just move on
                }

                var protoId = data.EntityPalette[spec.ProtoIndex];
                var spawned = Spawn(protoId, coords);
                _transform.SetLocalRotation(spawned, spec.Rotation);
                spec.OriginalEntity = GetNetEntity(spawned);
            }
        }

        Dirty(uid, data);
    }

    [AdminCommand(AdminFlags.Admin)]
    public void SnapshotGridCmd(IConsoleShell shell, string argstr, string[] args)
    {
        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteError("Couldn't parse entity.");
            return;
        }

        GenerateRepairData(uid);
    }
}

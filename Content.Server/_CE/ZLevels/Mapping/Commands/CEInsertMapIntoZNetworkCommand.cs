/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core;
using Content.Server._Mono.Planets;
using Content.Server.Administration;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server._CE.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CEInsertMapIntoZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private MapSystem _maps = default!;
    [Dependency] private CEZLevelsSystem _levels = default!;

    public override string Command => "znetwork-insert";
    public override string Description => "Link an existing map into a free depth of a Z-level network.";
    public override string Help => $"{Command} <network entity> <map ID> <depth>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var net) || !_entities.TryGetEntity(net, out var uid) ||
            !_entities.TryGetComponent<CEZMapNetworkComponent>(uid, out var network) ||
            !int.TryParse(args[1], out var mapId) || !_maps.TryGetMap(new MapId(mapId), out var map) ||
            !int.TryParse(args[2], out var depth))
        {
            shell.WriteError(Help);
            return;
        }

        if (_entities.HasComponent<PlanetMapGenerationComponent>(uid) ||
            !_levels.TryAddMapsIntoNetwork((uid.Value, network), new() { [map.Value] = depth }))
        {
            shell.WriteError($"Failed to insert map into z-network at depth {depth}.");
            return;
        }

        shell.WriteLine($"Linked map {mapId} at depth {depth}.");
    }
}

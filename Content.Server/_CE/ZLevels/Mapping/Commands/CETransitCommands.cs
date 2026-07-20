/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Administration;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Mapping.Prototypes;
using Content.Shared.Administration;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._CE.ZLevels.Mapping.Commands;

public abstract partial class CEBaseTransitCommand : LocalizedEntityCommands
{
    [Dependency] protected IEntityManager Entities = default!;
    [Dependency] protected CEZLevelsSystem ZLevel = default!;

    protected bool TryGetGrid(IConsoleShell shell, string arg, out Entity<MapGridComponent> grid)
    {
        grid = default;

        if (!NetEntity.TryParse(arg, out var netEnt) ||
            !Entities.TryGetEntity(netEnt, out var uid) ||
            !Entities.TryGetComponent<MapGridComponent>(uid, out var gridComp) ||
            Entities.HasComponent<MapComponent>(uid))
        {
            shell.WriteError($"{arg} is not a grid.");
            return false;
        }

        grid = (uid.Value, gridComp);
        return true;
    }

    protected CompletionResult TransitGridCompletions()
    {
        var options = new List<CompletionOption>();

        var query = Entities.EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (!Entities.HasComponent<CEZTransitMapComponent>(xform.MapUid))
                continue;

            var net = Entities.GetNetEntity(uid);
            options.Add(new CompletionOption(net.ToString(), Entities.ToPrettyString(uid)));
        }

        return CompletionResult.FromHintOptions(options, "<transiting grid>");
    }
}

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class CETransitEnterCommand : CEBaseTransitCommand
{
    public override string Command => "cez-transit-enter";
    public override string Description => "Force a grid into a transit map.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryGetGrid(shell, args[0], out var grid))
            return;

        var progress = 1f;
        if (args.Length == 2 && !float.TryParse(args[1], out progress))
        {
            shell.WriteError($"Invalid height {args[1]}.");
            return;
        }

        if (!ZLevel.TryEnterTransit(grid, args.Length == 2 ? progress : null))
        {
            shell.WriteError("Failed to enter transit (are you sure this grid is on a Z-level?)");
            return;
        }

        shell.WriteLine($"Grid {args[0]} is airborne.");
    }
}

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class CETransitSetCommand : CEBaseTransitCommand
{
    public override string Command => "cez-transit-set";
    public override string Description => "Set a transit map's current distance in the stack.";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? TransitGridCompletions()
            : CompletionResult.FromHint("<absolute altitude>");
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryGetGrid(shell, args[0], out var grid))
            return;

        if (!float.TryParse(args[1], out var altitude))
        {
            shell.WriteError($"Invalid altitude {args[1]}.");
            return;
        }

        if (!ZLevel.SetTransitAltitude(grid, altitude))
        {
            shell.WriteError("Grid is not in transit.");
            return;
        }
    }
}

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CETransitDebugCommand : CEBaseTransitCommand
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MapSystem _map = default!;

    public override string Command => "cez-transit-debug";
    public override string Description =>
        "Loads in a testing map and spawns a shuttle for testing Z-levels.";

    private const string DefaultZMapId = "Grasslands";
    private const int ShuttleDepth = 2;
    private static readonly ResPath DefaultShuttle = new("/SharedMaps/_Mono/Shuttles/bucket.yml");

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<CEZLevelMapPrototype>(proto: _proto),
                "<zMap id>"),
            2 => CompletionResult.FromHint("<shuttle path>"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var zMapId = args.Length >= 1 ? args[0] : DefaultZMapId;
        var shuttlePath = args.Length == 2 ? new ResPath(args[1]) : DefaultShuttle;

        if (!_proto.Resolve<CEZLevelMapPrototype>(zMapId, out var zMapProto))
        {
            shell.WriteError($"Unknown CEZLevelMapPrototype {zMapId}");
            return;
        }

        var network = ZLevel.CreateMapNetwork(zMapProto.Components);
        _meta.SetEntityName(network, $"Debug zNetwork: {zMapId}");

        var opts = new DeserializationOptions { InitializeMaps = true };

        var maps = new Dictionary<EntityUid, int>();
        var byDepth = new Dictionary<int, Entity<MapComponent>>();
        var depth = 0;

        foreach (var path in zMapProto.Maps)
        {
            if (!_mapLoader.TryLoadMap(path, out var mapEnt, out _, opts))
            {
                shell.WriteError($"Failed to load zNetwork map (depth {depth}): {path}!");
                Cleanup(byDepth, network);
                return;
            }

            maps.Add(mapEnt.Value, depth);
            byDepth[depth] = mapEnt.Value;
            _meta.SetEntityName(mapEnt.Value, $"Debug {zMapId} [{depth}]");
            depth++;
        }

        if (!ZLevel.TryAddMapsIntoNetwork(network, maps))
        {
            shell.WriteError("Failed to create zNetwork from loaded maps!");
            Cleanup(byDepth, network);
            return;
        }

        // i prefer being able to see don't you
        foreach (var (mapUid, _) in maps)
        {
            if (!Entities.TryGetComponent<LightCycleComponent>(mapUid, out var cycle))
                continue;

            var noon = (float)(cycle.Duration.TotalSeconds / 2d);
            var noonColor = SharedLightCycleSystem.GetColor((mapUid, cycle), cycle.OriginalColor, noon);

            cycle.OriginalColor = noonColor;
            cycle.Enabled = false;
            Entities.Dirty(mapUid, cycle);

            if (Entities.TryGetComponent<MapLightComponent>(mapUid, out var mapLight))
            {
                mapLight.AmbientLightColor = noonColor;
                Entities.Dirty(mapUid, mapLight);
            }
        }

        // A test ship, delivered to the sky.
        if (!byDepth.TryGetValue(ShuttleDepth, out var shuttleMap))
        {
            shell.WriteError($"{zMapId} has no layer {ShuttleDepth}; shuttle load skipped");
            return;
        }

        if (!_mapLoader.TryLoadGrid(shuttleMap.Comp.MapId, shuttlePath, out var shuttle, opts))
        {
            shell.WriteError($"Failed to load shuttle {shuttlePath}");
            return;
        }

        var shuttleNet = Entities.GetNetEntity(shuttle.Value.Owner);
        shell.WriteLine($"Loaded {zMapId}; shuttle {shuttleNet} on layer {ShuttleDepth} (map {shuttleMap.Comp.MapId})");
    }

    private void Cleanup(Dictionary<int, Entity<MapComponent>> byDepth, EntityUid network)
    {
        foreach (var (_, mapEnt) in byDepth)
        {
            _map.DeleteMap(mapEnt.Comp.MapId);
        }

        Entities.QueueDeleteEntity(network);
    }
}

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class CETransitLandCommand : CEBaseTransitCommand
{
    public override string Command => "cez-transit-land";
    public override string Description => "Immediately force a grid in transit to land on the nearest Z-layer.";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? TransitGridCompletions()
            : CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryGetGrid(shell, args[0], out var grid))
            return;

        if (!ZLevel.TryExitTransit(grid))
        {
            shell.WriteError("Failed to land (are you sure this grid is on a transitmap?)");
            return;
        }
    }
}

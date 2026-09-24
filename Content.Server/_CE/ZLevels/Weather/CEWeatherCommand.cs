/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Server.Administration;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Weather;
using Content.Shared.Administration;
using Content.Shared.Prototypes;
using Content.Shared.Weather;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Weather;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class CEWeatherCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IComponentFactory _compFactory = default!;

    public override string Command => "znetwork-weather";
    public override string Description => "Sets weather for all maps in zNetwork";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("cmd-weather-error-no-arguments"));
            return;
        }

        // get the target
        EntityUid? target;

        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out target))
        {
            shell.WriteError($"Unable to find entity {args[1]}");
            return;
        }

        if (!_entities.TryGetComponent<CEZMapNetworkComponent>(target, out var levelComp))
        {
            shell.WriteError($"Target entity doesnt have CEZLevelsNetworkComponent {args[1]}");
            return;
        }

        //Weather proto parse
        EntProtoId? weatherProtoId = args[1];
        if (args[1] == "null")
            weatherProtoId = null;

        WeatherPrototype? weatherProto = null;
        if (weatherProtoId != null && !_proto.TryIndex(weatherProtoId, out weatherProto))
        {
            shell.WriteError(Loc.GetString("cmd-weather-error-unknown-proto"));
            return;
        }

        //Time parsing
        TimeSpan? duration = null;
        if (args.Length == 3)
        {
            var curTime = _timing.CurTime;
            if (int.TryParse(args[2], out var durationInt))
            {
                duration = curTime + TimeSpan.FromSeconds(durationInt);
            }
            else
            {
                shell.WriteError(Loc.GetString("cmd-weather-error-wrong-time"));
            }
        }

        _entities.System<CEWeatherSystem>().SetWeather((target.Value, levelComp), weatherProto, duration);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = new List<CompletionOption>();
            var query = _entities.EntityQueryEnumerator<CEZMapNetworkComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out _, out var meta))
            {
                options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
            }
            return CompletionResult.FromHintOptions(options, "zNetwork net entity");
        }

        if (args.Length == 2)
        {
            var opts = new List<CompletionOption>();
            foreach (var proto in _proto.EnumeratePrototypes<WeatherPrototype>())
            {
                opts.Add(new CompletionOption(proto.ID));
            }
            return CompletionResult.FromHintOptions(opts, Loc.GetString("cmd-weather-hint-prototype"));
        }

        if (args.Length == 3)
        {
            return CompletionResult.FromHint("Duration in seconds");
        }

        return CompletionResult.Empty;
    }
}

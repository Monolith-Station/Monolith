using System.Linq;
using Content.Server._Mono.Planets;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Mono.Persistence;

[ToolshedCommand(Name = "persistence"), AdminCommand(AdminFlags.Admin)]
public sealed class PersistenceCommand : ToolshedCommand
{
    private PersistentProfileSystem? _persistence;

    [CommandImplementation("planet")]
    public string Planet([CommandArgument(typeof(LoadedPlanetNameParser))] string planetName,
        PlanetPersistenceAction action, int? minutesOrRoundId = null,
        [CommandArgument] bool confirm = false)
    {
        var planets = GetSys<PlanetPersistenceSystem>();
        var matches = planets.GetLoadedPlanets()
            .Where(planet => string.Equals(planet.Name, planetName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1)
            return Loc.GetString(matches.Length == 0 ? "planet-save-command-not-loaded" : "planet-save-command-ambiguous",
                ("planet", planetName));

        var planet = matches[0];
        return action switch
        {
            PlanetPersistenceAction.Save when minutesOrRoundId is null or > 0 && !confirm =>
                Loc.GetString(planets.ForceSave(planet.ID, minutesOrRoundId) ? "planet-save-command-ok" : "planet-save-command-failed",
                    ("planet", planet.Name)),
            PlanetPersistenceAction.Delete when minutesOrRoundId is >= 0 =>
                planets.DeleteRound(planet.ID, minutesOrRoundId.Value, confirm),
            _ => "persistence:planet <planetName> save [nextSaveMinutes] | persistence:planet <planetName> delete <roundId> [true]",
        };
    }

    [CommandImplementation("get")]
    public IReadOnlyList<string> Get(PersistenceDataType type, EntityUid player)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return type switch
        {
            PersistenceDataType.Flag when _persistence.TryGetFlags(player, out var flags) => flags,
            PersistenceDataType.Component when _persistence.TryGetComponents(player, out var components) =>
                components.Select(component => Format(component.Data, component.Sticky)).ToArray(),
            PersistenceDataType.Item when _persistence.TryGetItems(player, out var items) =>
                items.Select(item => Format(item.Data, item.Sticky)).ToArray(),
            _ => [],
        };
    }

    [CommandImplementation("add")]
    public bool AddFlag(PersistenceDataType type, EntityUid player, string key)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return type == PersistenceDataType.Flag && _persistence.AddFlag(player, key);
    }

    [CommandImplementation("add")]
    public bool AddComponent(
        [PipedArgument] IComponent component,
        PersistenceDataType type,
        EntityUid player,
        [CommandArgument] bool sticky = false)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return type == PersistenceDataType.Component && _persistence.AddComponent(player, component, sticky);
    }

    [CommandImplementation("add")]
    public bool AddItem(
        [PipedArgument] EntityUid item,
        PersistenceDataType type,
        EntityUid player,
        [CommandArgument] bool sticky = false)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return type == PersistenceDataType.Item && _persistence.AddItem(player, item, sticky);
    }

    [CommandImplementation("remove")]
    public bool RemoveFlag(PersistenceDataType type, EntityUid player, string key)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return type == PersistenceDataType.Flag && _persistence.RemoveFlag(player, key);
    }

    [CommandImplementation("remove")]
    public bool RemoveComponent(
        [PipedArgument] IComponent component,
        PersistenceDataType type,
        EntityUid player)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return type == PersistenceDataType.Component && _persistence.RemoveComponent(player, component);
    }

    [CommandImplementation("remove")]
    public bool RemoveItem(
        [PipedArgument] EntityUid item,
        PersistenceDataType type,
        EntityUid player)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return type == PersistenceDataType.Item &&
               _persistence.TrySerializeEntity(item, out var data) &&
               _persistence.RemoveItem(player, data);
    }

    private static string Format(string data, bool sticky)
        => sticky ? $"[sticky] {data}" : data;
}

public enum PlanetPersistenceAction : byte
{
    Save,
    Delete,
}

public enum PersistenceDataType : byte
{
    Flag,
    Component,
    Item,
}

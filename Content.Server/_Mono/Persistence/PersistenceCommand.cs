using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Mono.Persistence;

[ToolshedCommand(Name = "persistence"), AdminCommand(AdminFlags.Admin)]
public sealed class PersistenceCommand : ToolshedCommand
{
    private PersistentProfileSystem? _persistence;

    [CommandImplementation("get")]
    public IReadOnlyList<string> Get(PersistenceDataType type, EntityUid player)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return type switch
        {
            PersistenceDataType.Flag when _persistence.TryGetFlags(player, out var flags) => flags,
            PersistenceDataType.Component when _persistence.TryGetComponents(player, out var components) => components,
            PersistenceDataType.Item when _persistence.TryGetItems(player, out var items) => items,
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
        EntityUid player)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return type == PersistenceDataType.Component && _persistence.AddComponent(player, component);
    }

    [CommandImplementation("add")]
    public bool AddItem(
        [PipedArgument] EntityUid item,
        PersistenceDataType type,
        EntityUid player)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return type == PersistenceDataType.Item && _persistence.AddItem(player, item);
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
}

public enum PersistenceDataType : byte
{
    Flag,
    Component,
    Item,
}

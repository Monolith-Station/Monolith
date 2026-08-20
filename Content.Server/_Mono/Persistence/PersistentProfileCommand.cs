using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Mono.Persistence;

[ToolshedCommand(Name = "profileflag"), AdminCommand(AdminFlags.Admin)]
public sealed class PersistentProfileCommand : ToolshedCommand
{
    private PersistentProfileSystem? _persistence;

    [CommandImplementation("get")]
    public IReadOnlyList<string> Get([CommandArgument] EntityUid uid)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return _persistence.TryGetFlags(uid, out var flags) ? flags : [];
    }

    [CommandImplementation("add")]
    public bool Add(
        [CommandArgument] EntityUid uid,
        [CommandArgument] string key)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return _persistence.AddFlag(uid, key);
    }

    [CommandImplementation("remove")]
    public bool Remove(
        [CommandArgument] EntityUid uid,
        [CommandArgument] string key)
    {
        _persistence ??= GetSys<PersistentProfileSystem>();
        return _persistence.RemoveFlag(uid, key);
    }
}

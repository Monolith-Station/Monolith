using Content.Shared.Eui;
using Content.Shared.Preferences;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Persistence;

[Serializable, NetSerializable]
public sealed class FindPersistenceUser(string username) : EuiMessageBase
{
    public readonly string Username = username;
}

[Serializable, NetSerializable]
public sealed class PersistenceUserResult(string? username, PersistenceCharacter[] characters, bool failed = false) : EuiMessageBase
{
    public readonly string? Username = username;
    public readonly PersistenceCharacter[] Characters = characters;
    public readonly bool Failed = failed;
}

[Serializable, NetSerializable]
public sealed record PersistenceCharacter(int Slot, HumanoidCharacterProfile Preview, string[] Flags,
    PersistenceEntry[] Items, PersistenceEntry[] Components);

[Serializable, NetSerializable]
public sealed record PersistenceEntry(string? Name, bool Sticky, string Blob);

[Serializable, NetSerializable]
public sealed class PersistenceClipboardYaml(string yaml) : EntityEventArgs
{
    public readonly string Yaml = yaml;
}

[Serializable, NetSerializable]
public sealed class AddPersistenceEntry(int slot, bool item, string blob, bool sticky) : EuiMessageBase
{
    public const int MaxBlobLength = 128 * 1024;
    public readonly int Slot = slot;
    public readonly bool Item = item;
    public readonly string Blob = blob;
    public readonly bool Sticky = sticky;
}

[Serializable, NetSerializable]
public sealed class RemovePersistenceItem(int slot, int index) : EuiMessageBase
{
    public readonly int Slot = slot;
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class PersistenceEntriesUpdated(PersistenceCharacter? character, string? error = null) : EuiMessageBase
{
    public readonly PersistenceCharacter? Character = character;
    public readonly string? Error = error;
}

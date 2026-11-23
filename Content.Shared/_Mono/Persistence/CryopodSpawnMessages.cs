using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Persistence;

[Serializable, NetSerializable]
public sealed record CryopodSpawnOption(Guid Id, string Planet, string Job, int Slot, bool KeepsGear, bool CanWake);

[Serializable, NetSerializable]
public sealed class CryopodSpawnOptionsRequest : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class CryopodSpawnOptions(CryopodSpawnOption[] options) : EntityEventArgs
{
    public readonly CryopodSpawnOption[] Options = options;
}

[Serializable, NetSerializable]
public sealed class CryopodSpawnRequest(Guid id) : EntityEventArgs
{
    public readonly Guid Id = id;
}

[Serializable, NetSerializable]
public sealed class CryopodSpawnEuiState(CryopodSpawnOption[] options, string planets) : EuiStateBase
{
    public readonly CryopodSpawnOption[] Options = options;
    public readonly string Planets = planets;
}

[Serializable, NetSerializable]
public sealed class CryopodSpawnChoice(Guid? id) : EuiMessageBase
{
    public readonly Guid? Id = id;
}

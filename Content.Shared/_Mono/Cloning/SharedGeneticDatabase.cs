using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Cloning;

/// <summary>
/// basically just the RD server selection
/// </summary>
[Serializable, NetSerializable]
public enum GeneticDatabaseSelectUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed record GeneticDatabaseEntry(int Id, string Name);

[Serializable, NetSerializable]
public sealed class GeneticDatabaseSelectBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<GeneticDatabaseEntry> Databases;
    public readonly int? SelectedId;

    public GeneticDatabaseSelectBoundUserInterfaceState(List<GeneticDatabaseEntry> databases, int? selectedId)
    {
        Databases = databases;
        SelectedId = selectedId;
    }
}

[Serializable, NetSerializable]
public sealed class GeneticDatabaseOpenSelectMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class GeneticDatabaseSelectMessage : BoundUserInterfaceMessage
{
    public readonly int Id;

    public GeneticDatabaseSelectMessage(int id)
    {
        Id = id;
    }
}
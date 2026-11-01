using Content.Shared.Cloning.CloningConsole;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Cloning;

[Serializable, NetSerializable]
public enum GeneticCloningConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class GeneticCloningConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly string? ClonerBodyInfo;
    public readonly bool MindPresent;
    public readonly ClonerStatus CloningStatus;
    public readonly bool ClonerConnected;
    public readonly bool ClonerInRange;
    public readonly string? DatabaseName;
    public readonly List<GeneticRecordEntry> Records;

    public GeneticCloningConsoleBoundUserInterfaceState(string? clonerBodyInfo, bool mindPresent, ClonerStatus cloningStatus, bool clonerConnected, bool clonerInRange, string? databaseName, List<GeneticRecordEntry> records)
    {
        ClonerBodyInfo = clonerBodyInfo;
        MindPresent = mindPresent;
        CloningStatus = cloningStatus;
        ClonerConnected = clonerConnected;
        ClonerInRange = clonerInRange;
        DatabaseName = databaseName;
        Records = records;
    }
}

[Serializable, NetSerializable]
public sealed record GeneticRecordEntry(int Id, string Name, string Species, GeneticRecordStatus Status, TimeSpan ScannedAt);

[Serializable, NetSerializable]
public enum GeneticRecordStatus : byte
{
    Cloneable,
    Alive,
    Offline,
    /// <summary>
    /// now playing a different character.
    /// </summary>
    MovedOn,
    Lost,
}

[Serializable, NetSerializable]
public sealed class GeneticCloningConsoleCloneRecordMessage : BoundUserInterfaceMessage
{
    public readonly int Id;

    public GeneticCloningConsoleCloneRecordMessage(int id)
    {
        Id = id;
    }
}

[Serializable, NetSerializable]
public sealed class GeneticCloningConsoleDeleteRecordMessage : BoundUserInterfaceMessage
{
    public readonly int Id;

    public GeneticCloningConsoleDeleteRecordMessage(int id)
    {
        Id = id;
    }
}

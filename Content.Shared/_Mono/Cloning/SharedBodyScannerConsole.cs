using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Cloning;

[Serializable, NetSerializable]
public enum BodyScannerConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum BodyScanStatus : byte
{
    NoScanner,
    ScannerFar,
    Empty,
    Invalid,
    Ready,
}

[Serializable, NetSerializable]
public sealed class BodyScannerConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly string? OccupantName;
    public readonly BodyScanStatus Status;
    public readonly string? FailReason;
    public readonly bool OnFile;
    public readonly string? DatabaseName;

    public BodyScannerConsoleBoundUserInterfaceState(string? occupantName, BodyScanStatus status, string? failReason, bool onFile, string? databaseName)
    {
        OccupantName = occupantName;
        Status = status;
        FailReason = failReason;
        OnFile = onFile;
        DatabaseName = databaseName;
    }
}

[Serializable, NetSerializable]
public sealed class BodyScannerScanMessage : BoundUserInterfaceMessage
{
}

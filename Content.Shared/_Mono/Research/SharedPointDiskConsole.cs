using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Research;

[Serializable, NetSerializable]
public enum PointDiskConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class PointDiskConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool CanPrint1K;
    public bool CanPrint5K;
    public bool CanPrint10K;
    public int PointCost1K;
    public int PointCost5K;
    public int PointCost10K;
    public int ServerPoints;

    public PointDiskConsoleBoundUserInterfaceState(int serverPoints, int pointCost1K, int pointCost5K, int pointCost10K, bool canPrint1K, bool canPrint5K, bool canPrint10K)
    {
        CanPrint1K = canPrint1K;
        CanPrint5K = canPrint5K;
        CanPrint10K = canPrint10K;
        PointCost1K = pointCost1K;
        PointCost5K = pointCost5K;
        PointCost10K = pointCost10K;
        ServerPoints = serverPoints;
    }
}

[Serializable, NetSerializable]
public sealed class PointDiskConsolePrint1KDiskMessage : BoundUserInterfaceMessage
{

}

[Serializable, NetSerializable]
public sealed class PointDiskConsolePrint5KDiskMessage : BoundUserInterfaceMessage
{

}

[Serializable, NetSerializable]
public sealed class PointDiskConsolePrint10KDiskMessage : BoundUserInterfaceMessage
{

}

namespace Content.Server._Mono.Research.PointDiskPrinter.Components;

[RegisterComponent]
public sealed partial class PointDiskConsolePrintingComponent : Component
{
    public TimeSpan FinishTime;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Disk1K = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Disk5K = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Disk10K = false;
}

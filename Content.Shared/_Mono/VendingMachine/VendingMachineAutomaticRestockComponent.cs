namespace Content.Shared._Mono.VendingMachine;

[RegisterComponent]
public sealed partial class VendingMachineAutomaticRestockComponent : Component
{
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1800); // 30 minutes

    [DataField]
    public bool Replace = false;

    public TimeSpan LastRestock = TimeSpan.Zero;
}
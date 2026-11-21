namespace Content.Server._KS14.Packet.Components;

/// <summary>
/// This is used for dynamically loading modules into executors through item slots.
/// </summary>
[RegisterComponent]
public sealed partial class ExecutorModuleComponent : Component
{
    [DataField]
    public string ModuleName;
}

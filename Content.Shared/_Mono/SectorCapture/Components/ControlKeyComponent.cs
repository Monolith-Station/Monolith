using Content.Shared._Mono.Company;

namespace Content.Shared._Mono.SectorCapture.Components;
[RegisterComponent, NetworkedComponent]
public sealed partial class ControlKeyComponent : Component
{
    /// <summary>
    /// This component takes the company (or faction) of the source Control terminal and keeps it in memory to imprint onto Capture Terminals
    /// </summary>
    [Datafield]
    [AutoNetworkedField]
    public string Owner = "";
}

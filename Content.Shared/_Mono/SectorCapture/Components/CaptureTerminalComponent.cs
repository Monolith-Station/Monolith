using Content.Shared._Mono.Company;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Mono.SectorCapture.Components;
[RegisterComponent, NetworkedComponent]
public sealed partial class CaptureTerminalComponent : Component
{
    /// <summary>
    /// sets the current owner of the terminal
    /// </summary>
    [DataField]
    public string Owner = "";
    /// <summary>
    /// sets what class of POI terminal (ergo what class of POI) the terminal is assigned to: either Economy or Research
    /// </summary>
    [DataField]
    public string Class = "";
}

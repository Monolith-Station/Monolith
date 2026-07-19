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
    public string? Owner ;
    /// <summary>
    /// sets the class of capture terminal, which should correspond with the class of the POI that the terminal is on
    /// used to trigger specific radio calls/ popups (eventually a specific ui depending on POI class?)
    /// </summary>
    [DataField]
    public string? CaptureClass ;
}

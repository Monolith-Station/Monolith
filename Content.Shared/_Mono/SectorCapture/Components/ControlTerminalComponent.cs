using Content.Shared._Mono.Company;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Mono.SectorCapture.Components;
[RegisterComponent, NetworkedComponent]
public sealed partial class ControlTerminalComponent : Component
{
    /// <summary>
    /// sets the owner of the Control terminal, will not get changed in gameplay
    /// </summary>
    [DataField]
    public string? Owner ;
}

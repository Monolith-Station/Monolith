using Content.Shared._Mono.Company;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Mono.SectorCapture.Components;
[RegisterComponent, NetworkedComponent]
public sealed partial class ControlTerminalComponent : Component
{
    /// <summary>
    /// sets the owner of the Control terminal, so that it can display which POIs the owner (ergo faction) has captured
    /// </summary>
    [DataField]
    public string? Owner;
}

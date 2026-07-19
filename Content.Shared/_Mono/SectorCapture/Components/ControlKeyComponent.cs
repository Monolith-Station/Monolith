using Content.Shared._Mono.Company;

namespace Content.Shared._Mono.SectorCapture.Components;
[RegisterComponent, NetworkedComponent]
public sealed partial class ControlKeyComponent : Component
{
    /// <summary>
    /// This component sets the company of the encrypted capture key, and is used to switch state from Neutral/Hacked to Captured, with the owner built in
    /// </summary>
    [Datafield]
    [AutoNetworkedField]
    public string? Owner;
}

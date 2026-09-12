using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Tools.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SeparatoryFunnelComponent : Component
{
    [DataField]
    public float Delay = 2.5f;
}

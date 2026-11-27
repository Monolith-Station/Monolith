using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Planets;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ToroidalMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Size = 512f;
}

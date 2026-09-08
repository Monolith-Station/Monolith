using Robust.Shared.GameStates;

namespace Content.Shared._CE.Planets.Shields;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEPlanetShieldComponent : Component
{

    [DataField, AutoNetworkedField]
    public bool Active;
}

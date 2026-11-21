using Robust.Shared.GameStates;

namespace Content.Shared._CE.Planets.Hellpods;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEHellpodComponent : Component
{
    [AutoNetworkedField]
    public bool InFlight;

    [DataField]
    public float DropSpeed = 9f;

    [DataField]
    public float ImpactIntensity = 18f;
}

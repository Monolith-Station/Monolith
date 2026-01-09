using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Vessel;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VesselMusicComponent : Component
{
    [DataField, AutoNetworkedField]
    public string AmbientMusicPrototype = "";
}

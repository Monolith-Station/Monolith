using Content.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Crescent.Vessel;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VesselMusicComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<AmbientMusicPrototype> AmbientMusicPrototype;
}

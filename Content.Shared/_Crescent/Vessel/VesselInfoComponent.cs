using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Vessel;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VesselInfoComponent : Component
{
    /// <summary>
    /// exists to give the client the vessel's name. used for SpaceBiomeSystem to be fully clientside.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Description = "A metal coffin.";
}

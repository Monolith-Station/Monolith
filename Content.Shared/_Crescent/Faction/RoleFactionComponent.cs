using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.RoleFaction;

/// <summary>
/// Defines the faction attached to a role, for the purposes combat music.
/// </summary>

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class RoleFactionComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Faction = "";
}

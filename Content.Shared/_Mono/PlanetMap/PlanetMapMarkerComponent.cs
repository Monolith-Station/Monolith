using Robust.Shared.GameStates;

namespace Content.Shared._Mono.PlanetMap;

/// <summary>
/// Shows this entity as a labelled point on planet views
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlanetMapMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public LocId? Label;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#4FC3F7");

    [DataField, AutoNetworkedField]
    public bool ShowLabel = true;
}

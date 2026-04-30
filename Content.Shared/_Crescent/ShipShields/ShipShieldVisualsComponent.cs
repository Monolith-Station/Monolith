using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.ShipShields;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipShieldVisualsComponent : Component
{
    /// <summary>
    /// The color of this shield.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color ShieldColor = Color.White;

    /// <summary>
    /// The extra padding of this shield.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Padding = 50f;
}

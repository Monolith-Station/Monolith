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

    /// Mono
    /// <summary>
    /// Defines the arc of the shield in degrees starting from the frontal point of the ship, 360f is full coverage
    /// Setting it between 180 < x < 360 causes unintended behaviour, set only to 360 or less than or equal to 180
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ShieldArc = 360f;

    /// Mono
    /// <summary>
    /// The "direction" the shield is emitting from (0f is front, 180f is back)
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ShieldDirection = 0f;

    /// <summary>
    /// The extra padding of this shield.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Padding = 50f;
}

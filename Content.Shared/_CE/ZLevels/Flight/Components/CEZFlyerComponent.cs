/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.ZLevels.Flight.Components;

/// <summary>
/// A basic component that allows entities to fly between z-levels.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), Access(typeof(CESharedZFlightSystem))]
public sealed partial class CEZFlyerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public int TargetMapHeight = 0;

    [DataField]
    public float FlightSpeed = 1.5f;

    /// <summary>
    /// Stamina drained per second while flying.
    /// </summary>
    [DataField]
    public float HoverStaminaDrain;

    [DataField]
    public float FlightMoveSpeedModifier = 1f;

    /// <summary>
    /// Fixture mass at which the configured drain and speeds apply unchanged.
    /// Heavier flyers drain faster and fly slower; lighter ones the reverse.
    /// </summary>
    [DataField]
    public float ReferenceMass = 70f;

    [DataField]
    public float MinMassFactor = 0.5f;

    [DataField]
    public float MaxMassFactor = 2f;

    public TimeSpan NextStaminaDrain = TimeSpan.Zero;
}

using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.ShipShields.Components;

[RegisterComponent]
public sealed partial class ShipShieldRampingLoadComponent : Component
{
    /// <summary>
    /// The amount that the total load of this shield will be multiplied by when the interval passes.
    /// </summary>
    [DataField]
    public float Multiplier = 1.25f;

    /// <summary>
    /// The period of time between multiplications.
    /// </summary>
    [DataField]
    public TimeSpan MultiplicationInterval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The point in time in which to multiply this shield's load next.
    /// </summary>
    [DataField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// The radio channel this shield generator will give warnings over.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel;

    [DataField]
    public LocId Message = "biodome-shield-power-load-increasing";
}

using Content.Shared.Atmos; // Mono
using Content.Shared.Lathe;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Lathe.Components;

/// <summary>
/// This is used for a <see cref="LatheComponent"/> that releases heat into the surroundings while producing items.
/// </summary>
[RegisterComponent]
[Access(typeof(LatheSystem))]
public sealed partial class LatheHeatProducingComponent : Component
{
    /// <summary>
    /// The amount of energy produced each second when producing an item.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float EnergyPerSecond = 30000;

    // Mono
    /// <summary>
    /// Refuse to work if depositing the energy would bring the air to above this temperature.
    /// Doesn't apply if null.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float? TemperatureCap = null;

    // <Mono> - change to accumulator
    [DataField]
    public TimeSpan UpdateAccumulator;

    [DataField]
    public TimeSpan UpdateSpacing = TimeSpan.FromSeconds(1);

    // used for examine
    [DataField]
    public bool IsHot = false;
    // </Mono>

}

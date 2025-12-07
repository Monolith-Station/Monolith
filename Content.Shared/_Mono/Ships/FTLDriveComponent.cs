using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Ships;

/// <summary>
/// A component that enhances a shuttle's FTL range.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class FTLDriveComponent : Component
{
    /// <summary>
    /// The maximum FTL range this drive can achieve.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float Range = 512f;

    /// <summary>
    /// The FTL drive's cooldown between jumps before Mass Multiplier.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float Cooldown = 10f;


    /// <summary>
    /// The FTL jump duration before Mass Multiplier.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float HyperSpaceTime = 20f;

    /// <summary>
    /// The FTL duration until the jump starts before Mass Multiplier.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float StartupTime = 5.5f;

    /// <summary>
    /// Is the drive's FTL StartupTime, Travel Time, and Cooldown affected by the mass of the ship?
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool MassAffectedDrive = true;

    /// <summary>
    /// A multiplier of the effective mass a ship will have for mass calculations.
    /// Ships with mass zero will have half the FTL times.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float DriveMassMultiplier = 1f;

    /// <summary>
    /// Thermal signature produced while on cooldown or spooling up, if <see cref="ThermalSignatureComponent"/> is present.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float ThermalSignature = 2000000; // ~2.8km
}

namespace Content.Shared._Mono.Weapons.Ranged.Overheat;

[RegisterComponent]
public sealed partial class GunOverheatComponent : Component
{
    /// <summary>
    /// Initial firerate is divided by this upon reaching max heat.
    /// </summary>
    [DataField]
    public float FireRatePenalty = 2f;

    /// <summary>
    /// Initial spread is multiplied by this upon reaching max heat
    /// </summary>
    [DataField]
    public float SpreadPenalty = 2f;

    [DataField]
    public float PenaltyExponent = 0.25f;

    /// <summary>
    /// Maximum amount of heat this gun can accomodate
    /// </summary>
    [DataField]
    public float HeatCapacity = 100f;

    /// <summary>
    /// Current amount of heat
    /// </summary>
    [DataField]
    public float Heat = 0f;

    [DataField]
    public float HeatPerShot = 5f;

    /// <summary>
    /// Current amount of heat gets reduced every second by this value
    /// </summary>
    [DataField]
    public float HeatDissipation = 10f;


}

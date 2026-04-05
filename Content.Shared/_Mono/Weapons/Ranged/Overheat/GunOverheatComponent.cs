namespace Content.Shared._Mono.Weapons.Ranged.Overheat;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class GunOverheatComponent : Component
{
    [DataField]
    public float FireRatePenalty = 2f;

    [DataField]
    public float SpreadPenalty = 2f;

    [DataField]
    public float HeatCapacity = 100f;

    [DataField]
    public float Heat = 0f;

    [DataField]
    public float HeatPerShot = 5f;

    [DataField]
    public float HeatDissipation = 10f;
}

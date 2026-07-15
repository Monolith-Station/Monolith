using Robust.Shared.GameStates;

namespace Content.Shared._Mono.ArmorPlate;

[Flags]
public enum StaminaDamageSourceFlag
{
    Absorbed = 1 << 0,
    Amplified = 1 << 1,
    Raw = 1 << 2,
}

/// <summary>
/// Component for armor plates that can be inserted into compatible clothing.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ArmorPlateItemComponent : Component
{
    /// <summary>
    /// Maximum durability of this plate before destruction. Should match the destruction threshold in DestructibleComponent.
    /// Exclude DestructibleComponent and exclude durability field in YML to make the plate indestructible.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public int MaxDurability = -1;

    /// <summary>
    /// Walk speed modifier applied when this plate is active in worn clothing.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float WalkSpeedModifier = 1.0f;

    /// <summary>
    /// Sprint speed modifier applied when this plate is active in worn clothing.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float SprintSpeedModifier = 1.0f;

    /// <summary>
    /// Multiplier applied when converting damage to stamina damage.
	/// Amplified, absorbed, and raw damage are all distinct sources. Select accordingly with StaminaDamageSourceFlag
    /// </summary>
    [DataField]
    public float StaminaDamageMultiplier = 0f;

    /// <summary>
    /// Allows selection of Absorb and Amplified as stamina damage source. Defaults to absorbed.
    /// Example: StaminaDamageSource: Absorbed, Amplified
    /// Adding raw OVERRIDES the damagetype behavior: no double dipping.
    /// </summary>
    [DataField]
    public StaminaDamageSourceFlag StaminaDamageSource = StaminaDamageSourceFlag.Absorbed;

    /// <summary>
    /// How much of the raw damage is dealt to the plate, per damagetype
    /// This doesn't affect how much damage the plate absorbs, and is by default 1.0f for any damagetype with an absorption value
    /// Ex. 0.5 >> half of raw damage counts against plate hp, 2.0 >> 2x raw daamage counts against plate hp
    /// </summary>
    [DataField("damageToPlate")]
    public Dictionary<string, float> DamageToPlate = new();

    /// <summary>
    /// Absorption effect of the plate, by damagetype. Unintended effect past 1.0
	/// Can go negative which INCREASES damage taken. Negative values will still decrement armor durability.
    /// Ex. 0.2 >> 20% damage reduction, -0.2 >> 20% damage amplification
    /// </summary>
	[DataField("absorptionRatios")]
    public Dictionary<string, float> AbsorptionRatios = new();

}


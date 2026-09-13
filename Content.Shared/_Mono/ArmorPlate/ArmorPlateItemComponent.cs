using Robust.Shared.GameStates;

namespace Content.Shared._Mono.ArmorPlate;

/// <summary>
/// Component for armor plates that can be inserted into compatible clothing.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ArmorPlateItemComponent : Component
{
    /// <summary>
    /// Maximum durability of this plate before destruction. Should match the destruction threshold in DestructibleComponent.
    /// Exclude DestructibleComponent and omit this field in YML to make the plate indestructible.
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
    /// Stamina damage applied based on a multiplier and chosen portion of damage. Options are: Raw, Absorbed, or Amplified.
    /// Omit this field in YML to deal no stamina damage
    /// Adding raw OVERRIDES the damagetype behavior: no double dipping.
    /// </summary>
    [DataField("staminaDamageMultipliers")]
    public Dictionary<string, float> StaminaDamageMultipliers = new();

    /// <summary>
    /// How much of the raw damage is dealt to the plate, per damagetype. Needs an accompanying AbsorptionRatio to take effect.
    /// This doesn't affect how much damage the plate absorbs, and is by default 0f.
    /// Ex. 0.5 >> half of raw damage counts against plate hp, 2.0 >> 2x raw damage counts against plate hp
    /// </summary>
    [DataField("damageToPlate")]
    public Dictionary<string, float> DamageToPlate = new();

    /// <summary>
    /// Absorption effect of the plate, by damagetype. Unintended effect past 1.0
    /// This doesn't affect the durability cost of taking hits. Add an accompanying DamagetoPlate to adjust.
	/// Can go negative which INCREASES damage taken.
    /// Ex. 0.2 >> 20% damage reduction, -0.2 >> 20% damage amplification
    /// </summary>
	[DataField("absorptionRatios")]
    public Dictionary<string, float> AbsorptionRatios = new();

}


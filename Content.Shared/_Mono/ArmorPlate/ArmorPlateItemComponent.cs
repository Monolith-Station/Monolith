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
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public int MaxDurability = 100;

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
    /// Multiplier applied when converting absorbed piercing damage to stamina damage.
    /// </summary>
    [DataField]
    public float StaminaDamageMultiplier = 1.0f;
}


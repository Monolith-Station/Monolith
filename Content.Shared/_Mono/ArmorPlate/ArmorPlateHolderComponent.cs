using Robust.Shared.GameStates;

namespace Content.Shared._Mono.ArmorPlate;

/// <summary>
/// Component for clothes that can hold armor plates in their storage.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ArmorPlateHolderComponent : Component
{
    /// <summary>
    /// Reference to the currently active armor plate entity.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public EntityUid? ActivePlate;

    /// <summary>
    /// Whether to show a popup notification when the active plate is destroyed.
    /// </summary>
    [DataField]
    public bool ShowBreakPopup = true;

    /// <summary>
    /// Walk speed modifier from the currently active plate.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float WalkSpeedModifier = 1.0f;

    /// <summary>
    /// Sprint speed modifier from the currently active plate.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float SprintSpeedModifier = 1.0f;

    /// <summary>
    /// Stamina damage multiplier from the currently active plate.
    /// </summary>
    [DataField]
    public float StaminaDamageMultiplier = 1.0f;
}


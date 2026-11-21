using Robust.Shared.GameStates;

namespace Content.Shared._Mono.DualWield;

/// <summary>
/// Added to a user while they are dual-wielding two <see cref="LightWeaponComponent"/> items.
/// </summary>
/// <remarks>
/// "Left" and "right" here are index order within <c>HandsComponent.SortedHands</c>, not anatomy:
/// the first-indexed of the two hands is the left one. It attacks on <c>EngineKeyFunctions.Use</c>
/// and draws to the left of the cursor.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DualWieldComponent : Component
{
    /// <summary>
    /// Hand name of the first-indexed dual-wielded hand. Fires on left click.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string LeftHandId = string.Empty;

    /// <summary>
    /// Hand name of the second-indexed dual-wielded hand. Fires on right click.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string RightHandId = string.Empty;
}

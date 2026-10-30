using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Component for the Ironman trait.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IronmanComponent : Component
{
    /// <summary>
    /// The amount of money to deduct from the player's wallet on spawn (This maxes out at their role's starting gear.)
    /// </summary>
    [DataField]
    public int WalletDeduction = 2147483647; // 32-bit integer limit
}

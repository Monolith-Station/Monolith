using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Component for the Ironman trait.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IronmanComponent : Component
{
    [DataField]
    public bool BlockWithdraw = true;

    [DataField]
    public bool BlockDeposit = false;
}

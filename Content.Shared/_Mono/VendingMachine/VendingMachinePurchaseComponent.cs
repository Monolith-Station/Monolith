using Robust.Shared.GameStates;

namespace Content.Shared._Mono.VendingMachine;

/// <summary>
/// Component that tracks entities purchased from vending machines.
/// Used to apply pricing modifications when selling through cargo pallet consoles.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VendingMachinePurchaseComponent : Component
{
    /// <summary>
    /// The grid ID where this entity was purchased from a vending machine.
    /// Used to determine if the same-grid discount should apply.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid PurchaseGrid;

    /// <summary>
    /// The original purchase price from the vending machine.
    /// Stored for reference and potential future features.
    /// </summary>
    [DataField, AutoNetworkedField]
    public double OriginalPurchasePrice;
}

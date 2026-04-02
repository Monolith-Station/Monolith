using Content.Server.Cargo.Systems;
using Content.Shared._Mono.VendingMachine;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Robust.Shared.Map;

namespace Content.Server._Mono.VendingMachine;

/// <summary>
/// System that handles vending machine purchase tracking and pricing modifications.
/// </summary>
public sealed class VendingMachinePurchaseSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Note: We don't subscribe to PriceCalculationEvent to avoid interfering with other pricing
        // Instead, we provide specific methods for cargo systems to call when needed
    }

    /// <summary>
    /// Adds a VendingMachinePurchaseComponent to an entity when it's purchased from a vending machine.
    /// </summary>
    /// <param name="purchasedEntity">The entity that was purchased</param>
    /// <param name="vendingMachine">The vending machine it was purchased from</param>
    /// <param name="purchasePrice">The price paid for the entity</param>
    public void MarkAsPurchased(EntityUid purchasedEntity, EntityUid vendingMachine, double purchasePrice)
    {
        // Get the grid the vending machine is on
        var vendingTransform = Transform(vendingMachine);
        if (vendingTransform.GridUid == null)
            return;

        // Add the component to track this purchase
        var purchaseComponent = AddComp<VendingMachinePurchaseComponent>(purchasedEntity);
        purchaseComponent.PurchaseGrid = vendingTransform.GridUid.Value;
        purchaseComponent.OriginalPurchasePrice = purchasePrice;

        Dirty(purchasedEntity, purchaseComponent);
    }



    /// <summary>
    /// Gets the discounted price for a vending machine purchase if applicable.
    /// This method is called specifically by cargo systems to get the modified price.
    /// </summary>
    /// <param name="entity">The entity to check</param>
    /// <param name="currentGrid">The grid where the entity is being sold</param>
    /// <returns>The modified price if applicable, null otherwise</returns>
    public double? GetVendingMachineDiscountPrice(EntityUid entity, EntityUid currentGrid)
    {
        if (!TryComp<VendingMachinePurchaseComponent>(entity, out var component))
            return null;

        if (!TryComp<StaticPriceComponent>(entity, out var staticPrice))
            return null;

        // Only apply discount if on the same grid as original purchase
        if (component.PurchaseGrid == currentGrid)
        {
            return staticPrice.Price * 0.5; // 50% discount
        }

        return null;
    }

    /// <summary>
    /// Checks if an entity was purchased from a vending machine on the specified grid.
    /// </summary>
    /// <param name="entity">The entity to check</param>
    /// <param name="gridUid">The grid to check against</param>
    /// <returns>True if the entity was purchased from a vending machine on the specified grid</returns>
    public bool WasPurchasedOnGrid(EntityUid entity, EntityUid gridUid)
    {
        if (!TryComp<VendingMachinePurchaseComponent>(entity, out var component))
            return false;

        return component.PurchaseGrid == gridUid;
    }
}

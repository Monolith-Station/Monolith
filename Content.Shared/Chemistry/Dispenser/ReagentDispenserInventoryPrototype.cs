using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Chemistry.Dispenser
{
    /// <summary>
    /// Is simply a list of reagents defined in yaml. This can then be set as a
    /// <see cref="SharedReagentDispenserComponent"/>s <c>pack</c> value (also in yaml),
    /// to define which reagents it's able to dispense. Based off of how vending
    /// machines define their inventory.
    /// </summary>
    [Serializable, NetSerializable, Prototype]
    public sealed partial class ReagentDispenserInventoryPrototype : IPrototype
    {
        [DataField]
        public List<EntProtoId> Inventory = new();

        [ViewVariables, IdDataField]
        public string ID { get; private set; } = default!;
    }
}

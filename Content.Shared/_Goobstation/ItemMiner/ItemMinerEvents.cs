using Robust.Shared.Prototypes;

namespace Content.Shared._Goobstation.ItemMiner;

/// <summary>
/// Raised on an item miner to check whether it should work right now and to potentially override the prototype it should spawn.
/// </summary>
[ByRefEvent]
public record struct ItemMinerCheckEvent(EntProtoId Proto, bool Cancelled = false);

/// <summary>
/// Raised on an item miner when it mines an item.
/// Note that the item miner will attempt to merge the mined entity into nearby stacks AFTER firing this event unless NoStack is set.
/// </summary>
[ByRefEvent]
public record struct ItemMinedEvent(EntityUid Mined, bool NoStack = false);

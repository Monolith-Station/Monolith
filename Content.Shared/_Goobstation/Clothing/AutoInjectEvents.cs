// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;
using Content.Shared.Mobs;

namespace Content.Shared._Goobstation.Clothing;

public record struct ClothingAutoInjectRelayedEvent(EntityUid Target, MobState NewState) : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}


using System.Linq;
using Content.Shared._Mono.Weapons.Ranged.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared._Mono.Weapons.Ranged.Systems;

public sealed partial class GunLockMagazineDuringBurstSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _slots = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var gunQuery = EntityQueryEnumerator<GunComponent, GunLockMagazineDuringBurstComponent, ItemSlotsComponent>();
        while (gunQuery.MoveNext(out var uid, out var gun, out var gunLock, out var slots))
        {
            foreach (var slotId in gunLock.SlotIds)
            {
                if (!_slots.TryGetSlot(uid, slotId, out var slot, slots))
                    continue;

                if (slot.Locked == gun.BurstActivated) // things are already as they should be
                    continue;

                _slots.SetLock(uid, slotId, gun.BurstActivated, slots);
            }
        }
    }
}
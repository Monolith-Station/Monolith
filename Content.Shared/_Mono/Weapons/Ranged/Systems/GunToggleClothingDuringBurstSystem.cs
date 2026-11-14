using Content.Shared._Mono.Weapons.Ranged.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared._Mono.Weapons.Ranged.Systems;

public sealed partial class GunToggleClothingDuringBurstSystem : EntitySystem
{
    [Dependency] private ClothingSystem _clothing = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GunComponent, ClothingComponent, GunToggleClothingDuringBurstComponent>();
        while (query.MoveNext(out var uid, out var gun, out var clothing, out var toggle))
        {
            if (clothing.Slots == (gun.BurstActivated ? toggle.SlotsDuringBurst : toggle.IdleSlots))
                continue;

            _clothing.SetSlots(uid, gun.BurstActivated ? toggle.SlotsDuringBurst : toggle.IdleSlots, clothing);
        }
    }
}
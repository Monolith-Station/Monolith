using Content.Shared._Mono.ArmorPlate;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Inventory;
public sealed class ArmorPlateClient : SharedArmorPlateSystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlateProtectedComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
    }

    private void OnBeforeDamageChanged(Entity<PlateProtectedComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || !args.Damage.AnyPositive())
            return;

        if (args.Origin == null)
            return;

        if (!_inventory.TryGetSlots(ent, out var slots))
            return;

        if (!TryComp<InventoryComponent>(ent.Owner, out var inv))
            return;

        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(ent, slot.Name, out var equipped, inv))
                continue;


            if (!TryComp<ArmorPlateHolderComponent>(equipped, out var holder))
                continue;

            if (!TryGetActivePlate((equipped.Value, holder), out var plate))
                continue;

            CalcPlateDamages(args.Damage, plate.Comp, out var remainder, out _, out _);

            //We only need remainder damage so that the client doesn't mispredict crit or death upon mitigation of lethal damage
            args.Damage = remainder;
            if (remainder.Empty) args.Cancelled = true;
        }
    }
}

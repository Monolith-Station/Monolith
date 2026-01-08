using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.Eye;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;

namespace Content.Server._Mono.ShipRepair;

public sealed partial class ShipRepairSystem
{
    private void InitGhosts()
    {
        // need t-ray scanner eye for all repair tools because else it complains if you click on a tile with underfloor wire
        SubscribeLocalEvent<ShipRepairToolComponent, GotEquippedHandEvent>(OnScanHandEquipped);
        SubscribeLocalEvent<ShipRepairToolComponent, GotUnequippedHandEvent>(OnScanHandUnequipped);
        SubscribeLocalEvent<ShipRepairToolComponent, GotEquippedEvent>(OnScanEquipped);
        SubscribeLocalEvent<ShipRepairToolComponent, GotUnequippedEvent>(OnScanUnequipped);
        SubscribeLocalEvent<RepairDataEyeComponent, GetVisMaskEvent>(OnGetVis);
    }

    private void OnEquip(EntityUid user)
    {
        var comp = EnsureComp<RepairDataEyeComponent>(user);
        comp.Count++;

        if (comp.Count > 1)
            return;

        _eye.RefreshVisibilityMask(user);
    }

    private void OnUnequip(EntityUid user)
    {
        if (!TryComp(user, out RepairDataEyeComponent? comp))
            return;

        comp.Count--;

        if (comp.Count > 0)
            return;

        RemComp<RepairDataEyeComponent>(user);
        _eye.RefreshVisibilityMask(user);
    }

    private void OnScanHandUnequipped(Entity<ShipRepairToolComponent> ent, ref GotUnequippedHandEvent args)
    {
        OnUnequip(args.User);
    }

    private void OnScanHandEquipped(Entity<ShipRepairToolComponent> ent, ref GotEquippedHandEvent args)
    {
        OnEquip(args.User);
    }

    private void OnScanUnequipped(Entity<ShipRepairToolComponent> ent, ref GotUnequippedEvent args)
    {
        OnUnequip(args.Equipee);
    }

    private void OnScanEquipped(Entity<ShipRepairToolComponent> ent, ref GotEquippedEvent args)
    {
        OnEquip(args.Equipee);
    }

    private void OnGetVis(Entity<RepairDataEyeComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int)VisibilityFlags.Subfloor;
    }
}

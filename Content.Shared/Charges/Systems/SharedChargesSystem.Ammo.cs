// Mono - whole file
using Content.Shared.Charges.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Charges.Systems;

public abstract partial class SharedChargesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private void InitializeAmmo()
    {
        SubscribeLocalEvent<LimitedChargesAmmoComponent, ExaminedEvent>(OnAmmoExamine);
        SubscribeLocalEvent<LimitedChargesAmmoComponent, AfterInteractEvent>(OnAmmoAfterInteract);
        SubscribeLocalEvent<LimitedChargesAmmoComponent, InteractUsingEvent>(OnAmmoUsingInteract);
    }

    private void OnAmmoExamine(Entity<LimitedChargesAmmoComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var examineMessage = Loc.GetString("limited-charges-ammo-component-on-examine", ("charges", GetAmmoCharges(ent)));
        args.PushText(examineMessage);
    }

    private void RefillAmmo(Entity<LimitedChargesAmmoComponent> ent, EntityUid target, EntityUid user)
    {
        if (!TryComp<LimitedChargesComponent>(target, out var charges)
            || _whitelist.IsWhitelistFail(ent.Comp.Whitelist, target)
        )
            return;
        var count = Math.Min(charges.MaxCharges - charges.Charges, GetAmmoCharges(ent));
        if (count <= 0)
        {
            _popup.PopupClient(Loc.GetString("limited-charges-ammo-component-after-interact-full"), target, user);
            return;
        }

        _popup.PopupClient(Loc.GetString("limited-charges-ammo-component-after-interact-refilled"), target, user);
        AddCharges(target, TakeCharges(ent, count), charges);
    }

    private void OnAmmoUsingInteract(Entity<LimitedChargesAmmoComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_timing.IsFirstTimePredicted)
            return;

        args.Handled = true;
        RefillAmmo(ent, args.Used, args.User);
    }

    private void OnAmmoAfterInteract(Entity<LimitedChargesAmmoComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_timing.IsFirstTimePredicted)
            return;
        if (args.Target is not { } target)
            return;

        args.Handled = true;
        RefillAmmo(ent, target, args.User);
    }

    public int GetAmmoCharges(Entity<LimitedChargesAmmoComponent> ent)
    {
        if (TryComp<StackComponent>(ent, out var stack))
            return ent.Comp.Charges * stack.Count;
        return ent.Comp.Charges;
    }

    public int TakeCharges(Entity<LimitedChargesAmmoComponent> ent, int upTo)
    {
        if (!TryComp<StackComponent>(ent, out var stack))
        {
            var took = Math.Min(ent.Comp.Charges, upTo);
            ent.Comp.Charges -= took;
            Dirty(ent);
            if (ent.Comp.Charges == 0 && _net.IsServer)
                QueueDel(ent);
            return took;
        }

        var takeAmount = Math.Min(stack.Count, upTo / ent.Comp.Charges);
        _stack.SetCount(ent, stack.Count - takeAmount, stack);
        return takeAmount * ent.Comp.Charges;
    }
}

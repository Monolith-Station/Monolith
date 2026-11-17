using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.MAWC.Scrap;

public sealed partial class HotScrapSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HotScrapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HotScrapComponent, StackSplitEvent>(OnSplit);
        SubscribeLocalEvent<StackComponent, StackMergeEvent>(OnMerge);
    }

    private void OnMapInit(Entity<HotScrapComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.CooledAt != TimeSpan.Zero)
            return;

        ent.Comp.CooledAt = _timing.CurTime + ent.Comp.CoolTime;
        Dirty(ent);
    }

    // no free cooling by splitting the stack
    private void OnSplit(Entity<HotScrapComponent> ent, ref StackSplitEvent args)
    {
        var comp = ent.Comp;
        TryCopyComponent(ent, args.NewId, ref comp, out _);
    }

    // no free cooling by MERGING the stacks
    private void OnMerge(Entity<StackComponent> ent, ref StackMergeEvent args)
    {
        var hasHeat = TryComp<HotScrapComponent>(ent, out var heat);
        var donorHasHeat = TryComp<HotScrapComponent>(args.ToBeMerged, out var donorHeat);
        if (!hasHeat && !donorHasHeat)
            return;

        var recipientHeat = hasHeat ? GetHeat((ent.Owner, heat!)) : 0f;
        var incomingHeat = donorHasHeat ? GetHeat((args.ToBeMerged, donorHeat!)) : 0f;
        var mergedHeat = (recipientHeat * ent.Comp.Count + incomingHeat * args.Amount)
            / (ent.Comp.Count + args.Amount);

        if (!hasHeat && !TryCopyComponent(args.ToBeMerged, ent.Owner, ref donorHeat, out heat))
            return;

        heat!.CooledAt = _timing.CurTime + heat.CoolTime * mergedHeat;
        Dirty(ent.Owner, heat); // dirtyfield flags my IDE I'm not live testing fuck you
    }

    public float GetHeat(Entity<HotScrapComponent> ent)
    {
        var left = ent.Comp.CooledAt - _timing.CurTime;
        return Math.Clamp((float) (left / ent.Comp.CoolTime), 0f, 1f);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HotScrapComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.CooledAt)
            {
                RemCompDeferred<HotScrapComponent>(uid);
                continue;
            }

            if (now < comp.NextBurn)
                continue;

            comp.NextBurn = now + comp.BurnInterval;

            if (_container.TryGetContainingContainer((uid, null, null), out var container)
                && _hands.IsHolding(container.Owner, uid))
                Burn((uid, comp), container.Owner);
        }
    }

    private void Burn(Entity<HotScrapComponent> ent, EntityUid holder)
    {
        var damage = ent.Comp.Burn * GetHeat(ent);

        if (_inventory.TryGetInventoryEntity<DamageOnInteractProtectionComponent>(holder, out var gloves)
            && gloves.Comp is { } protection)
            damage = DamageSpecifier.ApplyModifierSet(damage, protection.DamageProtection);

        var dealt = _damageable.TryChangeDamage(holder, damage, origin: ent, targetPart: TargetBodyPart.Hands);
        if (dealt is { } d && d.GetTotal() > 0)
            _popup.PopupEntity(Loc.GetString("hot-scrap-burn", ("item", ent.Owner)), holder, holder, PopupType.SmallCaution);
    }
}

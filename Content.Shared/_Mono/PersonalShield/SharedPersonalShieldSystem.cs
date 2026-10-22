using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Projectiles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.PersonalShield;

public sealed partial class SharedPersonalShieldSystem : EntitySystem
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    [Dependency] private EntityQuery<ItemToggleComponent> _itemToggleQuery = default!;
    [Dependency] private EntityQuery<BatteryComponent> _batteryQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PersonalShieldComponent, PersonalShieldActionEvent>(OnAction);
        SubscribeLocalEvent<PersonalShieldComponent, InventoryRelayedEvent<BeforeDamageChangedEvent>>(OnBeforeDamageChanged);
        SubscribeLocalEvent<PersonalShieldComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<PersonalShieldComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<PersonalShieldComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnAction(Entity<PersonalShieldComponent> ent, ref PersonalShieldActionEvent ev)
    {
        _toggle.Toggle((ent, _itemToggleQuery.Comp(ent)), ev.Performer);
    }

    private void OnBeforeDamageChanged(Entity<PersonalShieldComponent> ent, ref InventoryRelayedEvent<BeforeDamageChangedEvent> ev)
    {
        ref var args = ref ev.Args;
        var shield = ent.Comp;
        if (args.Cancelled || !args.Damage.AnyPositive() || !shield.IsUp ||
            shield.Runtime.Charge <= 0f || !_inventory.InSlotWithFlags(ent.Owner, shield.Shield.RequiredSlot))
            return;

        var forceShatter = args.Tool is { } tool && HasComp<PersonalShieldBreakerComponent>(tool);
        var incoming = DamageSpecifier.ApplyModifierSet(args.Damage, shield.Shield.BlockDamageModifier)
            .GetTotal().Float();
        if (incoming <= 0f && !forceShatter)
            return;

        shield.Runtime.Charge = forceShatter ? 0f : MathF.Max(shield.Runtime.Charge - incoming, 0f);
        args.Cancelled = true;

        if (shield.Runtime.Charge <= 0f)
            Fracture(ent);
        else if (args.Origin is { } origin && IsPlayerDamage(origin))
            TriggerDamageFlare(ent);

        Dirty(ent);
    }

    private void OnProjectileHit(Entity<ProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!_net.IsClient || !_timing.IsFirstTimePredicted ||
            args.Shooter is not { } shooter || !HasComp<ActorComponent>(shooter) ||
            !args.Damage.AnyPositive())
        {
            return;
        }

        var slots = _inventory.GetSlotEnumerator(args.Target);
        while (slots.NextItem(out var item))
        {
            if (TryComp<PersonalShieldComponent>(item, out var shield) && shield.IsUp)
                TriggerDamageFlare((item, shield));
        }
    }

    private bool IsPlayerDamage(EntityUid origin)
    {
        return HasComp<ActorComponent>(origin) ||
               _inventory.TryGetContainingEntity(origin, out var holder) && HasComp<ActorComponent>(holder.Value);
    }

    private void TriggerDamageFlare(Entity<PersonalShieldComponent> ent)
    {
        if (ent.Comp.Visuals.DamageFlareTime <= 0f)
            return;

        ent.Comp.Runtime.DamageFlareUntil = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Visuals.DamageFlareTime);
        Dirty(ent);
    }

    private void OnActivateAttempt(Entity<PersonalShieldComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!_inventory.InSlotWithFlags(ent.Owner, ent.Comp.Shield.RequiredSlot))
        {
            args.Cancelled = true;
            args.Popup = Loc.GetString("personal-shield-toggle-not-equipped");
            return;
        }

        var runtime = ent.Comp.Runtime;
        if (runtime.Offline <= 0f && runtime.Shatter <= 0f)
            return;

        args.Cancelled = true;
        args.Popup = Loc.GetString("personal-shield-toggle-fractured",
            ("seconds", (int) MathF.Ceiling(runtime.Offline)));
    }

    private void OnUnequipped(Entity<PersonalShieldComponent> ent, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Shield.RequiredSlot) != ent.Comp.Shield.RequiredSlot)
            return;

        _toggle.TryDeactivate(ent.Owner, args.Equipee);
    }

    private void OnExamined(Entity<PersonalShieldComponent> ent, ref ExaminedEvent args)
    {
        var shield = ent.Comp;
        string msg;

        if (shield.Runtime.Shatter > 0f)
            msg = Loc.GetString("personal-shield-examine-broken");
        else if (shield.Runtime.Offline > 0f)
            msg = Loc.GetString("personal-shield-examine-offline",
                ("seconds", (int)MathF.Ceiling(shield.Runtime.Offline)));
        else if (shield.IsUp)
            msg = Loc.GetString("personal-shield-examine-up",
                ("percent", (int)MathF.Round(shield.Runtime.Charge / MathF.Max(shield.Shield.MaxCharge, 1f) * 100f)));
        else if (shield.Runtime.Form > 0f)
            msg = Loc.GetString("personal-shield-examine-spinup",
                ("percent", (int)MathF.Round(shield.Runtime.Form * 100f)));
        else
            msg = Loc.GetString("personal-shield-examine-down");

        args.PushMarkup(msg);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<PersonalShieldComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            var ent = (uid, shield);
            var before = shield.Runtime;
            var stats = GetStats(ent);

            if (shield.Runtime.Shatter > 0f)
            {
                shield.Runtime.Shatter += frameTime / MathF.Max(shield.Visuals.ShatterTime, 0.01f);
                if (shield.Runtime.Shatter >= 1f)
                {
                    shield.Runtime.Shatter = 0f;
                    shield.Runtime.Form = 0f;
                }

                DirtyIfChanged(ent, before);
                continue;
            }

            if (shield.Runtime.Offline > 0f)
            {
                shield.Runtime.Offline = MathF.Max(shield.Runtime.Offline - frameTime, 0f);
                DirtyIfChanged(ent, before);
                continue;
            }

            var running = _inventory.InSlotWithFlags(uid, shield.Shield.RequiredSlot)
                          && (!_itemToggleQuery.TryComp(uid, out var toggle) || toggle.Activated)
                          && TryDrawPower(ent, stats.PowerDraw, frameTime);

            var step = frameTime / MathF.Max(stats.SpinupTime, 0.01f);

            if (running)
            {
                shield.Runtime.Form = MathF.Min(shield.Runtime.Form + step, 1f);

                shield.Runtime.Charge = before.Form < 1f
                    ? shield.Shield.MaxCharge * shield.Runtime.Form
                    : MathF.Min(shield.Runtime.Charge + stats.RegenRate * frameTime, stats.MaxCharge);
            }
            else if (shield.Runtime.Form > 0f)
            {
                shield.Runtime.Form = MathF.Max(shield.Runtime.Form - step, 0f);
                shield.Runtime.Charge = stats.MaxCharge * shield.Runtime.Form;
            }

            DirtyIfChanged(ent, before);
        }
    }

    private bool TryDrawPower(Entity<PersonalShieldComponent> ent, float powerDraw, float frameTime)
    {
        if (powerDraw <= 0f || !_batteryQuery.HasComp(ent))
            return true;

        return _battery.TryUseCharge(ent, powerDraw * frameTime);
    }

    public GetPersonalShieldStatsEvent GetStats(Entity<PersonalShieldComponent> ent)
    {
        var ev = new GetPersonalShieldStatsEvent(ent.Comp.Shield);
        RaiseLocalEvent(ent, ref ev);
        return ev;
    }

    // Oops!
    public void Fracture(Entity<PersonalShieldComponent> ent)
    {
        TriggerDamageFlare(ent);
        ent.Comp.Runtime.Shatter = float.Epsilon;
        ent.Comp.Runtime.Charge = 0f;
        ent.Comp.Runtime.Offline = GetStats(ent).BreakCooldown;
        Dirty(ent, ent.Comp);
        _toggle.TryDeactivate(ent.Owner);
    }

    private void DirtyIfChanged(Entity<PersonalShieldComponent> ent, PersonalShieldRuntime before)
    {
        var now = ent.Comp.Runtime;
        if (MathHelper.CloseTo(before.Form, now.Form)
            && MathHelper.CloseTo(before.Shatter, now.Shatter)
            && MathHelper.CloseTo(before.Charge, now.Charge)
            && MathHelper.CloseTo(before.Offline, now.Offline))
        {
            return;
        }

        Dirty(ent, ent.Comp);
    }
}

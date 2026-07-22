using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Network;

namespace Content.Shared._Mono.PersonalShield;

public sealed partial class SharedPersonalShieldSystem : EntitySystem
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PersonalShieldComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<PersonalShieldComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<PersonalShieldComponent, ExaminedEvent>(OnExamined);
    }

    private void OnDamageModify(Entity<PersonalShieldComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        var shield = ent.Comp;
        if (!shield.IsUp || shield.Runtime.Charge <= 0f)
            return;

        var incoming = args.Args.Damage.GetTotal().Float();
        if (incoming <= 0f)
            return;

        var soaked = MathF.Min(incoming, shield.Runtime.Charge);
        shield.Runtime.Charge -= soaked;

        args.Args.Damage *= (incoming - soaked) / incoming;

        if (shield.Runtime.Charge <= 0f)
            Fracture(ent); // Uh oh.
        else
            Dirty(ent, shield);
    }

    private void OnActivateAttempt(Entity<PersonalShieldComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        var runtime = ent.Comp.Runtime;
        if (runtime.Offline <= 0f && runtime.Shatter <= 0f)
            return;

        args.Cancelled = true;
        args.Popup = Loc.GetString("personal-shield-toggle-fractured",
            ("seconds", (int) MathF.Ceiling(runtime.Offline)));
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
            var cfg = shield.Shield;

            if (shield.Runtime.Shatter > 0f)
            {
                shield.Runtime.Shatter += frameTime / MathF.Max(shield.ShatterTime, 0.01f);
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

            var running = (!TryComp<ItemToggleComponent>(uid, out var toggle) || toggle.Activated)
                          && TryDrawPower(ent, frameTime);

            var step = frameTime / MathF.Max(cfg.SpinupTime, 0.01f);

            if (running)
            {
                shield.Runtime.Form = MathF.Min(shield.Runtime.Form + step, 1f);

                shield.Runtime.Charge = shield.Runtime.Form < 1f
                    ? cfg.MaxCharge * shield.Runtime.Form
                    : MathF.Min(shield.Runtime.Charge + cfg.RegenRate * frameTime, cfg.MaxCharge);
            }
            else if (shield.Runtime.Form >= 1f)
            {
                shield.Runtime.Shatter = float.Epsilon;
                shield.Runtime.Charge = 0f;
            }
            else if (shield.Runtime.Form > 0f)
            {
                shield.Runtime.Form = MathF.Max(shield.Runtime.Form - step, 0f);
                shield.Runtime.Charge = cfg.MaxCharge * shield.Runtime.Form;
            }

            DirtyIfChanged(ent, before);
        }
    }

    private bool TryDrawPower(Entity<PersonalShieldComponent> ent, float frameTime)
    {
        if (ent.Comp.Shield.PowerDraw <= 0f || !HasComp<BatteryComponent>(ent))
            return true;

        return _battery.TryUseCharge(ent, ent.Comp.Shield.PowerDraw * frameTime);
    }

    // Oops!
    public void Fracture(Entity<PersonalShieldComponent> ent)
    {
        ent.Comp.Runtime.Shatter = float.Epsilon;
        ent.Comp.Runtime.Charge = 0f;
        ent.Comp.Runtime.Offline = ent.Comp.Shield.BreakCooldown;
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

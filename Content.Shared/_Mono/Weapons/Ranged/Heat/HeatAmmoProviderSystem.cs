using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.Weapons.Ranged.Heat;

public sealed partial class HeatAmmoProviderSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HeatAmmoProviderComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<HeatAmmoProviderComponent, GetAmmoCountEvent>(OnGetAmmoCount);
        SubscribeLocalEvent<HeatAmmoProviderComponent, CheckShootPrototypeEvent>(OnCheckPrototype);
        SubscribeLocalEvent<HeatAmmoProviderComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<HeatAmmoProviderComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<HeatAmmoProviderComponent, HeatVentDoAfterEvent>(OnVent);
        SubscribeLocalEvent<HeatAmmoProviderComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<HeatAmmoProviderComponent>();
        while (query.MoveNext(out var uid, out var heat))
        {
            // overheat is latched until a successful vent
            if (heat.Overheated || heat.Heat <= 0 || heat.HeatDissipation <= 0)
                continue;

            var fraction = heat.MaxHeat > 0 ? Math.Clamp(heat.Heat / heat.MaxHeat, 0f, 1f) : 0f;
            var rate = heat.HeatDissipation * Math.Max(0f, 1f + heat.HeatDissipationFactor * fraction);
            heat.Heat = Math.Max(0f, heat.Heat - rate * frameTime);
            Dirty(uid, heat);
        }
    }

    private void OnTakeAmmo(Entity<HeatAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        var heat = ent.Comp;
        if (heat.Overheated || heat.Venting)
            return;

        if (heat.MaxHeat <= 0 || heat.HeatPerShot <= 0)
            return;

        for (var i = 0; i < args.Shots && !heat.Overheated; i++)
        {
            var ammo = Spawn(heat.Proto, args.Coordinates);
            args.Ammo.Add((ammo, _gun.EnsureShootable(ammo)));
            heat.Heat = Math.Min(heat.MaxHeat, heat.Heat + heat.HeatPerShot);
            heat.Overheated = heat.Heat >= heat.MaxHeat;
        }

        Dirty(ent);
    }

    private void OnGetAmmoCount(Entity<HeatAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        if (ent.Comp.MaxHeat <= 0 || ent.Comp.HeatPerShot <= 0)
            return;

        args.Capacity = (int) MathF.Ceiling(ent.Comp.MaxHeat / ent.Comp.HeatPerShot);
        args.Count = ent.Comp.Overheated || ent.Comp.Venting
            ? 0
            : (int) MathF.Ceiling(Math.Max(0f, ent.Comp.MaxHeat - ent.Comp.Heat) / ent.Comp.HeatPerShot);
    }

    private void OnCheckPrototype(Entity<HeatAmmoProviderComponent> ent, ref CheckShootPrototypeEvent args)
    {
        _prototypes.TryIndex(ent.Comp.Proto, out var proto);
        args.ShootPrototype = proto;
    }

    private void OnUse(Entity<HeatAmmoProviderComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryVent(ent, args.User);
    }

    private void OnGetVerbs(Entity<HeatAmmoProviderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || _hands.GetActiveItem(args.User) != ent.Owner)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("gun-heat-vent"),
            Act = () => TryVent(ent, user),
        });
    }

    private void TryVent(Entity<HeatAmmoProviderComponent> ent, EntityUid user)
    {
        if (ent.Comp.Venting || ent.Comp.Heat <= 0 || _hands.GetActiveItem(user) != ent.Owner)
            return;

        if (_timing.CurTime < ent.Comp.NextVent)
            return;

        ent.Comp.Venting = true;
        Dirty(ent);
        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.VentDuration,
                new HeatVentDoAfterEvent(), ent, used: ent)
            {
                NeedHand = true,
                BreakOnDamage = true,
                MultiplyDelay = false,
            }))
        {
            ent.Comp.Venting = false;
            Dirty(ent);
        }
    }

    private void OnVent(Entity<HeatAmmoProviderComponent> ent, ref HeatVentDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Venting = false;
        Dirty(ent);

        if (args.Cancelled || !_hands.IsHolding(args.User, ent, out var hand))
            return;

        var fraction = ent.Comp.MaxHeat > 0 ? Math.Clamp(ent.Comp.Heat / ent.Comp.MaxHeat, 0f, 1f) : 0f;
        var burn = Math.Max(0f, ent.Comp.MaxHeatDamage) * fraction * (ent.Comp.Overheated ? 1.5f : 1f);
        ent.Comp.Heat = 0f;
        ent.Comp.Overheated = false;
        ent.Comp.NextVent = _timing.CurTime + ent.Comp.VentCooldown;
        _audio.PlayPredicted(ent.Comp.VentSound, ent, args.User);

        if (!_net.IsServer || burn <= 0)
            return;

        var target = hand.Location switch
        {
            HandLocation.Left => TargetBodyPart.LeftHand,
            HandLocation.Right => TargetBodyPart.RightHand,
            _ => TargetBodyPart.Hands,
        };
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Heat", FixedPoint2.New(burn));
        _damageable.TryChangeDamage(args.User, damage, origin: ent, targetPart: target, interruptsDoAfters: false);
    }

    private void OnExamined(Entity<HeatAmmoProviderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var fraction = ent.Comp.MaxHeat > 0 ? Math.Clamp(ent.Comp.Heat / ent.Comp.MaxHeat, 0f, 1f) : 0f;
        args.PushMarkup(Loc.GetString("gun-heat-examine", ("heat", Math.Round(fraction * 100f))));
        if (_timing.CurTime < ent.Comp.NextVent)
            args.PushMarkup(Loc.GetString("gun-heat-vent-cooldown",
                ("seconds", Math.Ceiling((ent.Comp.NextVent - _timing.CurTime).TotalSeconds))));
    }
}

using Content.Shared._Goobstation.Sandevistan;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared.Abilities;
using Content.Shared.Actions;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.Sandevistan;

public sealed class SandevistanSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SandevistanUserComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SandevistanUserComponent, ToggleSandevistanEvent>(OnToggle);
        SubscribeLocalEvent<SandevistanUserComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<SandevistanUserComponent, MeleeAttackEvent>(OnMeleeAttack);
        SubscribeLocalEvent<SandevistanUserComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SandevistanUserComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SandevistanUserComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Recharge has completed.
            if (comp.RechargeAt != null
                && _timing.CurTime >= comp.RechargeAt)
            {
                comp.RechargeAt = null;
            }

            // Nothing else needs to be updated while inactive.
            if (comp.Active == null)
                continue;

            // Automatically shut down when the active duration expires.
            if (comp.DisableAt != null
                && _timing.CurTime >= comp.DisableAt)
            {
                Disable(uid, comp);
                continue;
            }

            // Update visual trail.
            if (comp.Trail != null)
            {
                comp.Trail.Color = Color.FromHsv(new System.Numerics.Vector4(comp.ColorAccumulator % 100f / 100f, 1, 1, 1));
                comp.ColorAccumulator++;
                Dirty(uid, comp.Trail);
            }
        }
    }

    private void OnStartup(Entity<SandevistanUserComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.ActionUid = _actions.AddAction(ent, ent.Comp.ActionProto);
    }

    private void OnToggle(Entity<SandevistanUserComponent> ent, ref ToggleSandevistanEvent args)
    {
        args.Handled = true;

        // Currently active: turn it off.
        if (ent.Comp.Active != null)
        {
            _audio.Stop(ent.Comp.RunningSound);
            _audio.PlayEntity(ent.Comp.EndSound, ent, ent);
            Disable(ent, ent.Comp);
            return;
        }

        // Still recharging.
        if (ent.Comp.RechargeAt != null)
        {
            var remaining =
                ent.Comp.RechargeAt.Value - _timing.CurTime;

            if (remaining > TimeSpan.Zero)
            {
                _popup.PopupEntity(Loc.GetString("sandevistan-recharging",("time", Math.Ceiling(remaining.TotalSeconds))), ent, ent, PopupType.MediumCaution);
                return;
            }

            // Recharge has completed.
            ent.Comp.RechargeAt = null;
        }

        Activate(ent);
    }

    private void Activate(Entity<SandevistanUserComponent> ent)
    {
        ent.Comp.Active =
            EnsureComp<ActiveSandevistanUserComponent>(ent);

        ent.Comp.DisableAt =
            _timing.CurTime + ent.Comp.ActiveDuration;

        _speed.RefreshMovementSpeedModifiers(ent);

        // Add trail.
        if (!HasComp<TrailComponent>(ent))
        {
            var trail = AddComp<TrailComponent>(ent);

            trail.RenderedEntity = ent;
            trail.LerpTime = 0.1f;
            trail.LerpDelay = TimeSpan.FromSeconds(4);
            trail.Lifetime = 10;
            trail.Frequency = 0.07f;
            trail.AlphaLerpAmount = 0.2f;
            trail.MaxParticleAmount = 25;

            ent.Comp.Trail = trail;
        }

        // Add dog vision.
        if (!HasComp<DogVisionComponent>(ent))
            ent.Comp.Overlay = AddComp<DogVisionComponent>(ent);

        // Play activation sound.
        var audio = _audio.PlayEntity(
            ent.Comp.StartSound,
            ent,
            ent);

        if (audio.HasValue)
            ent.Comp.RunningSound = audio.Value.Entity;
    }

    private void OnRefreshSpeed(Entity<SandevistanUserComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Active == null)
            return;

        args.ModifySpeed( ent.Comp.MovementSpeedModifier, ent.Comp.MovementSpeedModifier);
    }

    private void OnMeleeAttack(Entity<SandevistanUserComponent> ent, ref MeleeAttackEvent args)
    {
        if (ent.Comp.Active == null
            || !TryComp<MeleeWeaponComponent>(args.Weapon, out var weapon))
        {
            return;
        }

        var rate = weapon.NextAttack - _timing.CurTime;

        // weapon.AttackRate breaks things when multiple systems
        // modify NextAttack.
        weapon.NextAttack -=
            rate - rate / ent.Comp.AttackSpeedModifier;
    }

    private void OnMobStateChanged(Entity<SandevistanUserComponent> ent, ref MobStateChangedEvent args)
    {
        // A mob state change immediately shuts down
        // an active Sandevistan.
        if (ent.Comp.Active != null)
            Disable(ent, ent.Comp);
    }

    private void OnShutdown( Entity<SandevistanUserComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Active != null)
            Disable(ent, ent.Comp);
        Del(ent.Comp.ActionUid);
    }

    private void Disable( Entity<SandevistanUserComponent> ent)
    {
        Disable(ent.Owner, ent.Comp);
    }

    private void Disable(EntityUid uid, SandevistanUserComponent comp)
    {
        if (comp.Active == null)
            return;

        RemComp<ActiveSandevistanUserComponent>(uid);

        comp.Active = null;
        comp.DisableAt = null;
        comp.ColorAccumulator = 0;
        _audio.Stop(comp.RunningSound);
        comp.RunningSound = null;
        _speed.RefreshMovementSpeedModifiers(uid);
        // Start recharge when the Sandevistan shuts down.
        comp.RechargeAt = _timing.CurTime + comp.RechargeDuration;

        // Remove dog vision.
        if (comp.Overlay != null)
        {
            RemComp<DogVisionComponent>(uid);
            comp.Overlay = null;
        }

        // Remove trail.
        if (comp.Trail != null)
        {
            RemComp<TrailComponent>(uid);
            comp.Trail = null;
        }
    }
}
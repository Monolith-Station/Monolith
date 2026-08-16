using System.Numerics;
using Content.Shared._Obelisk.Species.Components;
using Content.Shared.Actions;
using Content.Shared.Camera;
using Content.Shared.Damage.Systems;
using Content.Shared.Drugs;
using Content.Shared.Drunk;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.BlackFlash;

public sealed partial class BlackFlashSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;


    [Dependency] private EntityQuery<BlackFlashComponent> _blackFlashQuery = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BlackFlashComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BlackFlashComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlackFlashComponent, BlackFlashActionEvent>(OnAction);
        SubscribeLocalEvent<BlackFlashArmedComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ActorComponent, MeleeHitEvent>(OnMeleeHitNormal);

        Subs.CVar(_cfg, BlackFlashCVars.BlackFlashChance, val => BaseProcChance = val, true);
        Subs.CVar(_cfg, BlackFlashCVars.DamageMultiplier, val => NormalDamageMultiplier = val, true);
    }

    [DataField] public float NormalDamageMultiplier = 2.5f;
    [DataField] public float BaseProcChance = 0.00005f;
    [DataField] private readonly BlackFlashComponent _procSettings = new();

    private static float SwingRoll(uint tick, int user, int weapon)
    {
        var h = (uint)user * 2654435761u ^ (uint)weapon * 2246822519u ^ tick * 3266489917u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        h *= 3266489917u;
        h ^= h >> 16;
        return h / (float)uint.MaxValue;
    }

    private void OnMeleeHitNormal(Entity<ActorComponent> ent, ref MeleeHitEvent args)
    {
        if (args.Handled || !args.IsHit)
            return;

        var damageMult = 1;

        if (_blackFlashQuery.HasComp(args.User))
            return; // people that can do it at will dont get a random proc because im mean

        var blackFlashChance = BaseProcChance;

        if (HasComp<DrunkComponent>(args.User))
            blackFlashChance *= 2;

        if (HasComp<SeeingRainbowsComponent>(args.User))
            blackFlashChance *= 2;

        if (TryComp<BlackFlashLastHitComponent>(args.User, out var lastHitComponent))
        {
            blackFlashChance *= 10;
        }

        if (HasComp<HydrakinComponent>(args.User))
        {
            blackFlashChance *= _cfg.GetCVar<float>("mono.fun.blackflash_hydrakin_chance_multiplier");
            damageMult *= 2; // the birds have cursed energy
        }

        if (SwingRoll(_timing.CurTick.Value, GetNetEntity(args.User).Id, GetNetEntity(args.Weapon).Id) >= blackFlashChance)
        {
            RemComp<BlackFlashLastHitComponent>(args.User); // Failed.
            return;
        }

        if (args.HitEntities.Count == 0)
        {
            Fumble(args.User, _procSettings, args.Direction);
            RemComp<BlackFlashLastHitComponent>(args.User); // Failed.
            return;
        }
        if (lastHitComponent != null)
            args.BonusDamage += args.BaseDamage * ((lastHitComponent.CurrentDamageMultiplier - 1f) * damageMult);
        else
            args.BonusDamage += args.BaseDamage * ((NormalDamageMultiplier - 1f) * damageMult);

        var origin = _transform.GetWorldPosition(args.User);
        foreach (var target in args.HitEntities)
        {
            Detonate(args.User, _procSettings, target, Facing(args.User, _transform.GetWorldPosition(target) - origin));
        }

        _audio.PlayPredicted(_procSettings.HitSound, args.User, args.User);
        var newLastHit = EnsureComp<BlackFlashLastHitComponent>(args.User);
        newLastHit.CurrentDamageMultiplier *= 1.5f;
    }

    private void OnMapInit(Entity<BlackFlashComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnShutdown(Entity<BlackFlashComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnAction(Entity<BlackFlashComponent> ent, ref BlackFlashActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_melee.TryGetWeapon(ent, out var weapon, out _))
            return;

        if (HasComp<BlackFlashArmedComponent>(weapon))
        {
            RemComp<BlackFlashArmedComponent>(weapon);
            _actions.SetToggled(ent.Comp.ActionEntity, false);
            return;
        }

        var armed = EnsureComp<BlackFlashArmedComponent>(weapon);
        armed.User = ent;
        Dirty(weapon, armed);

        _actions.SetToggled(ent.Comp.ActionEntity, true);
    }

    private void OnMeleeHit(Entity<BlackFlashArmedComponent> weapon, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.User != weapon.Comp.User)
            return;

        if (!_blackFlashQuery.TryComp(args.User, out var flash))
            return;

        if (args.HitEntities.Count == 0)
        {
            Lapse(weapon, weapon.Comp, args.Direction);
            return;
        }

        RemComp<BlackFlashArmedComponent>(weapon);

        args.BonusDamage += args.BaseDamage * (flash.DamageMultiplier - 1f);

        var origin = _transform.GetWorldPosition(args.User);
        foreach (var target in args.HitEntities)
        {
            Detonate(args.User, flash, target, Facing(args.User, _transform.GetWorldPosition(target) - origin));
        }

        _actions.SetToggled(flash.ActionEntity, false);
        _actions.SetCooldown(flash.ActionEntity, flash.HitCooldown);
        _audio.PlayPredicted(flash.HitSound, args.User, args.User);
    }

    private void Detonate(EntityUid user, BlackFlashComponent settings, EntityUid target, Vector2 direction)
    {
        _stun.TryParalyze(target, settings.StunTime, false);

        var hitstop = EnsureComp<BlackFlashHitstopComponent>(target);
        hitstop.LaunchAt = _timing.CurTime + settings.Hitstop;
        hitstop.Direction = direction;
        hitstop.Distance = settings.ThrowDistance;
        hitstop.Speed = settings.ThrowSpeed;
        hitstop.User = user;
        Dirty(target, hitstop);

        _recoil.KickCamera(target, direction * 0.35f);
        _recoil.KickCamera(user, direction * 0.2f);

        var frames = EnsureComp<BlackFlashImpactFramesComponent>(user);
        frames.Start = _timing.CurTime;
        Dirty(user, frames);

        SpawnBurst(settings.HitEffect, user, direction);

        _stamina.TakeStaminaDamage(user, settings.StaminaCost);
    }

    private void Lapse(EntityUid weapon, BlackFlashArmedComponent armed, Vector2? direction)
    {
        var user = armed.User;
        RemComp<BlackFlashArmedComponent>(weapon);

        if (!_blackFlashQuery.TryComp(user, out var flash))
            return;

        _actions.SetToggled(flash.ActionEntity, false);
        _actions.SetCooldown(flash.ActionEntity, flash.MissCooldown);

        Fumble(user, flash, direction);
    }

    private void Fumble(EntityUid user, BlackFlashComponent settings, Vector2? direction)
    {
        _audio.PlayPredicted(settings.MissSound, user, user);
        SpawnBurst(settings.MissEffect, user, Facing(user, direction ?? Vector2.Zero));
    }

    private void SpawnBurst(string proto, EntityUid at, Vector2 direction)
    {
        var effect = PredictedSpawnAttachedTo(proto, Transform(at).Coordinates);
        _transform.SetWorldRotation(effect, new Angle(direction));

        var burst = Comp<BlackFlashEffectComponent>(effect);
        burst.Start = _timing.CurTime;
        burst.Seed = (_timing.CurTick.Value * 31u + (uint)Math.Abs(GetNetEntity(at).Id)) % 1000u / 1000f;
        Dirty(effect, burst);
    }

    private Vector2 Facing(EntityUid user, Vector2 direction)
    {
        return direction.LengthSquared() > 0f
            ? direction.Normalized()
            : _transform.GetWorldRotation(user).ToWorldVec();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var armed = EntityQueryEnumerator<BlackFlashArmedComponent>();
        while (armed.MoveNext(out var uid, out var comp))
        {
            if (!_blackFlashQuery.HasComp(comp.User))
            {
                RemCompDeferred<BlackFlashArmedComponent>(uid);
                continue;
            }

            if (!_melee.TryGetWeapon(comp.User, out var current, out _) || current == uid)
                continue;

            RemCompDeferred<BlackFlashArmedComponent>(uid);
            var moved = EnsureComp<BlackFlashArmedComponent>(current);
            moved.User = comp.User;
            Dirty(current, moved);
        }

        var frames = EntityQueryEnumerator<BlackFlashImpactFramesComponent>();
        while (frames.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.Start + comp.Total)
                RemCompDeferred<BlackFlashImpactFramesComponent>(uid);
        }

        var frozen = EntityQueryEnumerator<BlackFlashHitstopComponent, PhysicsComponent>();
        while (frozen.MoveNext(out var uid, out var comp, out var body))
        {
            if (now < comp.LaunchAt)
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
                _physics.SetAngularVelocity(uid, 0f, body: body);
                continue;
            }

            RemCompDeferred<BlackFlashHitstopComponent>(uid);
            _throwing.TryThrow(uid, comp.Direction * comp.Distance, comp.Speed, comp.User,
                unanchor: ThrowingUnanchorStrength.Unanchorable);
        }
    }
}

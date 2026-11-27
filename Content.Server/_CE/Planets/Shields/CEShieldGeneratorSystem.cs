using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared._CE.Planets.Shields;
using Content.Shared.Damage;
using Content.Shared.Emp;
using Content.Shared._FarHorizons.StarSystem;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._CE.Planets.Shields;

public sealed partial class CEShieldGeneratorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private CEPlanetShieldSystem _shield = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private CESharedShieldGeneratorSystem _projector = default!;
    private static readonly TimeSpan UiUpdateInterval = TimeSpan.FromSeconds(0.5);

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(CESharedShieldGeneratorSystem));
        SubscribeLocalEvent<CEShieldGeneratorComponent, CEShieldGeneratorStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<CEShieldGeneratorComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<CEShieldGeneratorComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<CEShieldGeneratorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CEShieldGeneratorComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    private void OnUiOpened(Entity<CEShieldGeneratorComponent> ent, ref BoundUIOpenedEvent args)
    {
        ent.Comp.NextUiUpdate = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CEShieldGeneratorComponent, PowerConsumerComponent>();
        while (query.MoveNext(out var uid, out var gen, out var consumer))
        {
            var xform = Transform(uid);
            if (xform.MapUid is not { } mapUid || !_shield.TryGetPlanetForMap(mapUid, out var planet))
            {
                consumer.DrawRate = 0f;
                AbortOffPlanet((uid, gen));
                UpdateUi((uid, gen), consumer, now, onPlanet: false);
                continue;
            }
            if (gen.ShieldedPlanet is { } guarded && guarded != planet)
                AbortOffPlanet((uid, gen));

            if (gen.MoonOnly && (!TryComp<PlanetBodyComponent>(planet, out var body)
                || body.ParentBody is not { } parent || !HasComp<PlanetBodyComponent>(parent)))
            {
                consumer.DrawRate = 0f;
                AbortOffPlanet((uid, gen));
                UpdateUi((uid, gen), consumer, now, objectTooLarge: true);
                continue;
            }

            consumer.DrawRate = gen.Stage switch
            {
                CEShieldGeneratorStage.Charging => gen.MaxDrawRate,
                CEShieldGeneratorStage.Active when gen.Charge < gen.MaxCharge => gen.MaxDrawRate,
                CEShieldGeneratorStage.Cooldown => 0f,
                _ => gen.DrawRate,
            };

            switch (gen.Stage)
            {
                case CEShieldGeneratorStage.Charging:
                    gen.Charge = Math.Min(gen.Charge + consumer.ReceivedPower * frameTime, gen.MaxCharge);
                    if (gen.Charge >= gen.MaxCharge)
                        StartSpinup((uid, gen), now);
                    break;

                case CEShieldGeneratorStage.Active:
                    var surplus = consumer.ReceivedPower - gen.DrawRate;
                    gen.Charge = Math.Clamp(gen.Charge + surplus * frameTime, 0f, gen.MaxCharge);
                    if (surplus < 0f && gen.Charge <= 0f)
                        Fail((uid, gen), now);
                    break;

            }

            UpdateUi((uid, gen), consumer, now);
        }
    }

    private void UpdateUi(Entity<CEShieldGeneratorComponent> ent, PowerConsumerComponent consumer, TimeSpan now,
        bool onPlanet = true, bool objectTooLarge = false)
    {
        if (now < ent.Comp.NextUiUpdate)
            return;

        ent.Comp.NextUiUpdate = now + UiUpdateInterval;
        if (!_ui.IsUiOpen(ent.Owner, CEShieldGeneratorUiKey.Key))
            return;

        var gen = ent.Comp;
        var eta = !onPlanet || objectTooLarge ? -1f : gen.Stage switch
        {
            CEShieldGeneratorStage.Charging when consumer.ReceivedPower > 0f
                => (gen.MaxCharge - gen.Charge) / consumer.ReceivedPower,
            CEShieldGeneratorStage.SpinningUp => (float) (gen.SpinupEndsAt - now).TotalSeconds,
            CEShieldGeneratorStage.Active when gen.DrawRate - consumer.ReceivedPower > 0f
                => gen.Charge / (gen.DrawRate - consumer.ReceivedPower),
            CEShieldGeneratorStage.Cooldown => (float) (gen.CooldownEndsAt - now).TotalSeconds,
            _ => -1f,
        };

        _ui.SetUiState(ent.Owner, CEShieldGeneratorUiKey.Key, new CEShieldGeneratorBuiState
        {
            Stage = gen.Stage,
            Charge = gen.Charge,
            MaxCharge = gen.MaxCharge,
            ReceivedPower = consumer.ReceivedPower,
            WantedPower = consumer.DrawRate,
            EtaSeconds = eta,
            OnPlanet = onPlanet,
            ObjectTooLarge = objectTooLarge,
        });
    }

    private void AbortOffPlanet(Entity<CEShieldGeneratorComponent> ent)
    {
        if (ent.Comp.Stage is not (CEShieldGeneratorStage.SpinningUp or CEShieldGeneratorStage.Active))
            return;

        _projector.SetStage(ent, CEShieldGeneratorStage.Charging);
    }

    private void StartSpinup(Entity<CEShieldGeneratorComponent> ent, TimeSpan now)
    {
        ent.Comp.SpinupDamageTaken = 0f;
        ent.Comp.ChargeSoundStream = _audio.PlayPvs(ent.Comp.ChargeSound, ent)?.Entity;
        _projector.BeginSpinup(ent, now);
    }

    private void OnStageChanged(Entity<CEShieldGeneratorComponent> ent, ref CEShieldGeneratorStageChangedEvent args)
    {
        if (args.Current == CEShieldGeneratorStage.Active)
        {
            Fire(ent);
            return;
        }

        if (args.Previous == CEShieldGeneratorStage.SpinningUp)
            ent.Comp.ChargeSoundStream = _audio.Stop(ent.Comp.ChargeSoundStream);

        if (args.Previous == CEShieldGeneratorStage.Active)
            LowerShield(ent);
    }

    private void Fire(Entity<CEShieldGeneratorComponent> ent)
    {
        var xform = Transform(ent);
        if (xform.MapUid is { } mapUid
            && _shield.TryGetPlanetForMap(mapUid, out var planet)
            && _shield.SetShieldActive(planet, true))
        {
            ent.Comp.ShieldedPlanet = planet;
        }
        else
        {
            Log.Warning($"Shield generator {ToPrettyString(ent)} fired but no planet resolved for its map; no field raised.");
            AbortOffPlanet(ent);
            return;
        }

        ent.Comp.ChargeSoundStream = null;
    }

    private void Fail(Entity<CEShieldGeneratorComponent> ent, TimeSpan now)
    {
        ent.Comp.Charge = 0f;
        _projector.BeginCooldown(ent, now);
        _audio.PlayPvs(ent.Comp.FailSound, ent);
    }

    private void OnDamaged(Entity<CEShieldGeneratorComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Stage != CEShieldGeneratorStage.SpinningUp)
            return;

        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        ent.Comp.SpinupDamageTaken += args.DamageDelta.GetTotal().Float();
        if (ent.Comp.SpinupDamageTaken >= ent.Comp.SpinupDamageThreshold)
            Detonate(ent);
    }

    private void OnEmpPulse(Entity<CEShieldGeneratorComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.Detonating)
            return;
        if (ent.Comp.Stage is not (CEShieldGeneratorStage.SpinningUp or CEShieldGeneratorStage.Active))
            return;
        args.Affected = true;
        args.Disabled = true;
        Detonate(ent);
    }

    private void OnShutdown(Entity<CEShieldGeneratorComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.ChargeSoundStream = _audio.Stop(ent.Comp.ChargeSoundStream);
        if (ent.Comp.Stage != CEShieldGeneratorStage.Active)
            return;

        LowerShield(ent);
        _audio.PlayPvs(ent.Comp.FailSound, Transform(ent).Coordinates);
    }

    private void Detonate(Entity<CEShieldGeneratorComponent> ent)
    {
        if (ent.Comp.Detonating)
            return;
        ent.Comp.Detonating = true;

        ent.Comp.ChargeSoundStream = _audio.Stop(ent.Comp.ChargeSoundStream);

        _explosion.QueueExplosion(ent, ExplosionSystem.DefaultExplosionPrototypeId,
            totalIntensity: 300f, slope: 2f, maxTileIntensity: 30f);
        QueueDel(ent);
    }

    private void LowerShield(Entity<CEShieldGeneratorComponent> ent)
    {
        var planet = ent.Comp.ShieldedPlanet;
        ent.Comp.ShieldedPlanet = null;
        if (planet is not { } uid || TerminatingOrDeleted(uid))
            return;

        var query = EntityQueryEnumerator<CEShieldGeneratorComponent>();
        while (query.MoveNext(out var other, out var generator))
        {
            if (other != ent.Owner && generator.Stage == CEShieldGeneratorStage.Active &&
                generator.ShieldedPlanet == uid && !TerminatingOrDeleted(other))
                return;
        }

        _shield.SetShieldActive(uid, false);
    }
}

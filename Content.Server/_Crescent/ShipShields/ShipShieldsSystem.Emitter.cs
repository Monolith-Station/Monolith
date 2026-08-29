using Content.Server._Crescent.ShipShields.Components;
using Content.Shared._Crescent.ShipShields;
using Content.Server.Power.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Components;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Station.Systems;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Content.Server.Explosion.Components;
using Content.Shared._CE.Camera;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Crescent.ShipShields;

public partial class ShipShieldsSystem
{
    private const float MAX_EMP_DAMAGE = 10000f;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    public void InitializeEmitters()
    {
        SubscribeLocalEvent<ShipShieldEmitterComponent, ShieldDeflectedEvent>(OnShieldDeflected);
        SubscribeLocalEvent<ShipShieldEmitterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ShipShieldEmitterComponent, ComponentRemove>(OnRemoved);
    }


    private void OnRemoved(Entity<ShipShieldEmitterComponent> owner, ref ComponentRemove remove)
    {
        var parent = Transform(owner.Owner).GridUid;
        if (parent is null)
            return;
        UnshieldEntity(parent.Value, null);
    }

    private void OnShieldDeflected(EntityUid uid, ShipShieldEmitterComponent component, ShieldDeflectedEvent args)
    {
        if (TryComp<EmpOnTriggerComponent>(args.Deflected, out var emp))
        {
            component.Damage += Math.Clamp(emp.EnergyConsumption, 0f, MAX_EMP_DAMAGE);
            _trigger.Trigger(args.Deflected);
        }

        if (TryComp<ExplosiveComponent>(args.Deflected, out var exp) && _prototypeManager.TryIndex(exp.ExplosionType, out var type))
        {
            component.Damage += exp.TotalIntensity * (float)type.DamagePerIntensity.GetTotal();
        }

        component.Damage += (float)args.Projectile.Damage.GetTotal();
        args.Projectile.ProjectileSpent = true;

        QueueDel(args.Deflected);
    }

    public bool TryAbsorbCrash(EntityUid grid, float totalIntensity)
    {
        if (!TryComp<ShipShieldedComponent>(grid, out var shielded) ||
            shielded.Source is not { } source ||
            !TryComp<ShipShieldEmitterComponent>(source, out var emitter) ||
            emitter.Shield is null)
            return false;

        var damage = totalIntensity;
        if (_prototypeManager.TryIndex<ExplosionPrototype>(ExplosionSystem.DefaultExplosionPrototypeId, out var explosion))
            damage *= (float) explosion.DamagePerIntensity.GetTotal();

        emitter.Damage += damage;
        AdjustEmitterLoad(source, emitter);
        if (emitter.Damage <= emitter.DamageLimit)
            return true;

        emitter.OverloadAccumulator = MathF.Max(emitter.OverloadAccumulator, emitter.CrashRespoolTime);
        emitter.CrashRespooling = true;

        var filter = Filter.BroadcastGrid(grid);
        _audio.PlayGlobal(emitter.CrashShutdownSound, filter, true, emitter.CrashShutdownSound.Params);
        RaiseNetworkEvent(new RadialShakeEvent
        {
            Duration = 1.5f,
            Amplitude = Math.Clamp(totalIntensity / 1500f, 0.6f, 1.2f),
        }, filter);

        UnshieldEntity(grid);
        emitter.Shield = null;
        emitter.Shielded = null;
        return true;
    }

    private void OnExamined(EntityUid uid, ShipShieldEmitterComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("shield-emitter-examine", ("basedraw", component.BaseDraw), ("additional", CalculateLoadDamage(component))));
        if (component.CrashRespooling && component.OverloadAccumulator > 0f)
            args.PushMarkup(Loc.GetString("shield-emitter-examine-crash-respool", ("seconds", MathF.Ceiling(component.OverloadAccumulator))));
        if (HasComp<ShipShieldDisabledGridComponent>(Transform(uid).GridUid))
            args.PushMarkup(Loc.GetString("shield-emitter-examine-invalid-grid"));
    }

    private static float CalculateLoadDamage(ShipShieldEmitterComponent emitter)
    {
        return (float)Math.Clamp(Math.Pow(emitter.Damage, emitter.DamageExp) * emitter.PowerModifier, 0f, emitter.MaxDraw);
    }

    private void AdjustEmitterLoad(EntityUid uid, ShipShieldEmitterComponent? emitter = null, ApcPowerReceiverComponent? receiver = null)
    {
        if (!Resolve(uid, ref emitter, ref receiver))
            return;

        receiver.Load = emitter.BaseDraw + CalculateLoadDamage(emitter);
    }
}

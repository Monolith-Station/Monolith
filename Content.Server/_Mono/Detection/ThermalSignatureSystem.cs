using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Detection;
using Content.Shared._Mono.Ships;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Detection;

/// <summary>
///     Handles the logic for thermal signatures.
/// </summary>
public sealed class ThermalSignatureSystem : EntitySystem
{
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5f);
    private TimeSpan _nextUpdateTime;

    private const float HeatThreshold = 1f;

    private EntityQuery<ThermalSignatureComponent> _sigQuery;
    private EntityQuery<GunComponent> _gunQuery;

    private Dictionary<EntityUid, ThermalSignatureComponent> _gridCompMap = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnMapGridMapInit);
        SubscribeLocalEvent<ThermalSignatureComponent, ComponentShutdown>(OnThermalSignatureShutdown);

        // some of this could also be handled in shared but there's no point since PVS is a thing
        SubscribeLocalEvent<MachineThermalSignatureComponent, GetThermalSignatureEvent>(OnMachineGetSignature);
        SubscribeLocalEvent<PassiveThermalSignatureComponent, GetThermalSignatureEvent>(OnPassiveGetSignature);

        SubscribeLocalEvent<ThermalSignatureComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<PowerSupplierComponent, GetThermalSignatureEvent>(OnPowerGetSignature);
        SubscribeLocalEvent<ThrusterComponent, GetThermalSignatureEvent>(OnThrusterGetSignature);
        SubscribeLocalEvent<FTLDriveComponent, GetThermalSignatureEvent>(OnFTLGetSignature);

        _sigQuery = GetEntityQuery<ThermalSignatureComponent>();
        _gunQuery = GetEntityQuery<GunComponent>();
    }

    private void OnMapGridMapInit(Entity<MapGridComponent> entity, ref MapInitEvent _)
    {
        if (!_sigQuery.TryComp(entity, out var sigComp))
            sigComp = EnsureComp<ThermalSignatureComponent>(entity);

        sigComp.TotalHeat = 0f;
        _gridCompMap.Add(entity.Owner, sigComp);
    }

    private void OnThermalSignatureShutdown(Entity<ThermalSignatureComponent> entity, ref ComponentShutdown _)
    {
        _gridCompMap.Remove(entity);
    }

    private void OnGunShot(Entity<ThermalSignatureComponent> ent, ref GunShotEvent args)
    {
        if (_gunQuery.TryComp(ent, out var gun))
            ent.Comp.StoredHeat += gun.ShootThermalSignature;
    }

    private void OnMachineGetSignature(Entity<MachineThermalSignatureComponent> ent, ref GetThermalSignatureEvent args)
    {
        if (_power.IsPowered(ent.Owner))
            args.Signature += ent.Comp.Signature;
    }

    private void OnPassiveGetSignature(Entity<PassiveThermalSignatureComponent> ent, ref GetThermalSignatureEvent args)
    {
        args.Signature += ent.Comp.Signature;
    }

    private void OnPowerGetSignature(Entity<PowerSupplierComponent> ent, ref GetThermalSignatureEvent args)
    {
        args.Signature += ent.Comp.CurrentSupply * ent.Comp.HeatSignatureRatio;
    }

    private void OnThrusterGetSignature(Entity<ThrusterComponent> ent, ref GetThermalSignatureEvent args)
    {
        if (ent.Comp.Firing)
            args.Signature += ent.Comp.Thrust * ent.Comp.HeatSignatureRatio;
    }

    private void OnFTLGetSignature(Entity<FTLDriveComponent> ent, ref GetThermalSignatureEvent args)
    {
        var xform = Transform(ent);
        if (!TryComp<FTLComponent>(xform.GridUid, out var ftl))
            return;

        if (ftl.State == FTLState.Starting || ftl.State == FTLState.Cooldown)
            args.Signature += ent.Comp.ThermalSignature;
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdateTime)
            return;

        _nextUpdateTime = _timing.CurTime + UpdateInterval;

        var interval = (float)UpdateInterval.TotalSeconds;

        var query = EntityQueryEnumerator<ThermalSignatureComponent>();
        while (query.MoveNext(out var uid, out var sigComp))
        {
            var ev = new GetThermalSignatureEvent();
            RaiseLocalEvent(uid, ref ev);

            sigComp.StoredHeat += ev.Signature * interval;

            // threshold should be really small value so client/server desync wont be a problem
            if (sigComp.StoredHeat < HeatThreshold)
                continue;

            sigComp.StoredHeat *= MathF.Pow(sigComp.HeatDissipation, interval);

            if (_gridCompMap.ContainsKey(uid))
            {
                sigComp.TotalHeat += sigComp.StoredHeat;
            }
            else
            {
                var xform = Transform(uid);
                sigComp.TotalHeat = sigComp.StoredHeat;
                if (xform.GridUid != null && _gridCompMap.TryGetValue(xform.GridUid.Value, out var gridSig))
                    gridSig.TotalHeat += sigComp.StoredHeat;
            }

            Dirty(uid, sigComp);
        }

    }
}

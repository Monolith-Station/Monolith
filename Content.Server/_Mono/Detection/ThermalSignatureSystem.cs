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
using System;

namespace Content.Server._Mono.Detection;

/// <summary>
///     Handles the logic for thermal signatures.
/// </summary>
public sealed class ThermalSignatureSystem : EntitySystem
{
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    private TimeSpan _updateInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _updateAccumulator = TimeSpan.FromSeconds(0);
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<ThermalSignatureComponent> _sigQuery;
    private EntityQuery<GunComponent> _gunQuery;

    public override void Initialize()
    {
        base.Initialize();

        // some of this could also be handled in shared but there's no point since PVS is a thing
        SubscribeLocalEvent<MachineThermalSignatureComponent, GetThermalSignatureEvent>(OnMachineGetSignature);
        SubscribeLocalEvent<PassiveThermalSignatureComponent, GetThermalSignatureEvent>(OnPassiveGetSignature);

        SubscribeLocalEvent<ThermalSignatureComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<PowerSupplierComponent, GetThermalSignatureEvent>(OnPowerGetSignature);
        SubscribeLocalEvent<ThrusterComponent, GetThermalSignatureEvent>(OnThrusterGetSignature);
        SubscribeLocalEvent<FTLDriveComponent, GetThermalSignatureEvent>(OnFTLGetSignature);

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _sigQuery = GetEntityQuery<ThermalSignatureComponent>();
        _gunQuery = GetEntityQuery<GunComponent>();
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
        _updateAccumulator += TimeSpan.FromSeconds(frameTime);
        if (_updateAccumulator < _updateInterval)
            return;
        _updateAccumulator -= _updateInterval;

        var interval = (float)_updateInterval.TotalSeconds;

        var gridQuery = EntityQueryEnumerator<MapGridComponent>();
        while (gridQuery.MoveNext(out var uid, out _))
        {
            var sigComp = EnsureComp<ThermalSignatureComponent>(uid);
            sigComp.TotalHeat = 0f;
        }

        var query = EntityQueryEnumerator<ThermalSignatureComponent>();
        while (query.MoveNext(out var uid, out var sigComp))
        {
            var ev = new GetThermalSignatureEvent(interval);
            RaiseLocalEvent(uid, ref ev);
            sigComp.StoredHeat += ev.Signature * interval;
            sigComp.StoredHeat *= MathF.Pow(sigComp.HeatDissipation, interval);
            if (_gridQuery.HasComp(uid))
            {
                sigComp.TotalHeat += sigComp.StoredHeat;
            }
            else
            {
                var xform = Transform(uid);
                sigComp.TotalHeat = sigComp.StoredHeat;
                if (xform.GridUid != null && _sigQuery.TryComp(xform.GridUid, out var gridSig))
                    gridSig.TotalHeat += sigComp.StoredHeat;
            }
        }

        var gridQuery2 = EntityQueryEnumerator<MapGridComponent, ThermalSignatureComponent>();
        while (gridQuery2.MoveNext(out var uid, out _, out var sigComp))
        {
            Dirty(uid, sigComp); // sync to client
        }
    }
}

using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Mono.Ships;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Lock;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Ships;

public sealed partial class ShipShieldProjectorSystem : EntitySystem
{
    [Dependency] private SharedShipShieldShellSystem _shells = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private LockSystem _locks = default!;
    [Dependency] private PowerNetSystem _power = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private TimeSpan _nextUiUpdate;

    public override void Initialize()
    {
        UpdatesBefore.Add(typeof(SharedShipShieldShellSystem));
        UpdatesBefore.Add(typeof(ShipShieldCollisionSystem));
        SubscribeLocalEvent<ShipShieldProjectorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ShipShieldProjectorComponent, LockToggledEvent>(OnLockToggled);
        Subs.BuiEvents<ShipShieldProjectorComponent>(ShipShieldProjectorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
        });
    }

    public override void Update(float frameTime)
    {
        var refreshUi = _timing.CurTime >= _nextUiUpdate;
        if (refreshUi)
            _nextUiUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.25);

        var query = EntityQueryEnumerator<ShipShieldProjectorComponent>();
        while (query.MoveNext(out var uid, out var projector))
        {
            TryComp(uid, out ApcPowerReceiverComponent? receiver);
            var requested = projector.RequestsPower && receiver != null;
            var recharging = requested && projector.CanRecharge(_timing.CurTime);
            if (receiver != null)
                receiver.Load = requested
                    ? projector.BaseDraw + (recharging ? projector.RechargeDraw : 0f)
                    : 0f;

            var draw = receiver switch
            {
                null or { PowerDisabled: true } => 0f,
                { NeedsPower: false } => receiver.Load,
                _ => receiver.PowerReceived,
            };
            var rate = recharging && projector.RechargeDraw > 0f
                ? projector.RechargeRate * Math.Clamp((draw - projector.BaseDraw) / projector.RechargeDraw, 0f, 1f)
                : 0f;
            var hasPower = requested && !receiver!.PowerDisabled && draw + 0.5f >= projector.BaseDraw;
            var startupPower = (projector.State == ShipShieldShellState.Off ||
                projector.State == ShipShieldShellState.Starting && projector.StartupSpoolAt.HasValue) &&
                receiver != null && _power.CanSupplyApcLoad(receiver, projector.BaseDraw);
            if (projector.HasOperatingPower != hasPower || projector.PoweredRechargeRate != rate ||
                projector.StartupPowerAvailable != startupPower)
            {
                projector.HasOperatingPower = hasPower;
                projector.StartupPowerAvailable = startupPower;
                projector.PoweredRechargeRate = rate;
                Dirty(uid, projector);
            }

            if (refreshUi)
                UpdateUi((uid, projector));
        }
    }

    private void OnUiOpened(Entity<ShipShieldProjectorComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);

    private void OnLockToggled(Entity<ShipShieldProjectorComponent> ent, ref LockToggledEvent args) => UpdateUi(ent);

    private void UpdateUi(Entity<ShipShieldProjectorComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, ShipShieldProjectorUiKey.Key))
            return;

        TryComp(ent, out ApcPowerReceiverComponent? receiver);
        _ui.SetUiState(ent.Owner, ShipShieldProjectorUiKey.Key, new ShipShieldProjectorUiState(
            _locks.IsLocked(ent.Owner), receiver?.PowerReceived ?? 0f, receiver?.Load ?? 0f));
    }

    public void AbsorbHit(Entity<ShipShieldProjectorComponent> projector, DamageSpecifier damage, float empEnergy = 0f)
    {
        if (!_shells.IsSolid(projector.Comp))
            return;

        var comp = projector.Comp;
        var stallSeconds = Math.Clamp(empEnergy / 1_000_000f * comp.EmpStallSecondsPerMegajoule,
            0f, ShipShieldProjectorComponent.MaxEmpStallSeconds);
        if (stallSeconds > 0f)
        {
            var until = _timing.CurTime + TimeSpan.FromSeconds(stallSeconds);
            if (until > comp.RechargeStalledUntil)
            {
                comp.RechargeStalledUntil = until;
                Dirty(projector);
            }
        }

        var cost = (float) DamageSpecifier.ApplyModifierSet(damage, comp.DamageModifiers).GetTotal() + comp.GetEmpDamage(empEnergy);
        if (cost > 0f)
            _shells.SpendCapacity(projector, cost);
    }

    public bool TryAbsorbCrash(EntityUid grid, float damage, out bool overloaded)
    {
        overloaded = false;
        Entity<ShipShieldProjectorComponent>? best = null;
        var query = EntityQueryEnumerator<ShipShieldProjectorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var projector, out var xform))
        {
            if (xform.GridUid == grid && _shells.IsSolid(projector) &&
                (best is not { } candidate || projector.Capacity > candidate.Comp.Capacity))
                best = (uid, projector);
        }

        if (best is not { } source)
            return false;

        overloaded = _shells.SpendCapacity(source, damage, crash: true);
        return true;
    }

    private void OnExamined(Entity<ShipShieldProjectorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var size = ent.Comp.Shield.Radii * 2f;
        args.PushMarkup(Loc.GetString("mono-ship-shield-examine-size", ("x", size.X), ("y", size.Y), ("z", size.Z)), priority: -10);
    }
}

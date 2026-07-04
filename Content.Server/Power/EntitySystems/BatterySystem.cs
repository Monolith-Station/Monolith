using System.Diagnostics.CodeAnalysis;
using Content.Server.Cargo.Systems;
using Content.Shared.Emp;
using Content.Server.Power.Components;
using Content.Shared.Examine;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Rejuvenate;
using Content.Shared.Timing;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Utility;
using Robust.Shared.Timing;
using Content.Server._NF.Power.Components; // Frontier

namespace Content.Server.Power.EntitySystems
{
    [UsedImplicitly]
    public sealed partial class BatterySystem : SharedBatterySystem
    {
        [Dependency] protected IGameTiming Timing = default!;

        [Dependency] private SharedContainerSystem _containers = default!; // WD EDIT

        // Mono
        private float _updateInterval = 1f;
        private float _updateAccumulator = 0f;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ExaminableBatteryComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<PowerNetworkBatteryComponent, RejuvenateEvent>(OnNetBatteryRejuvenate);
            SubscribeLocalEvent<BatteryComponent, RejuvenateEvent>(OnBatteryRejuvenate);
            SubscribeLocalEvent<BatteryComponent, PriceCalculationEvent>(CalculateBatteryPrice);
            SubscribeLocalEvent<BatteryComponent, ChangeChargeEvent>(OnChangeCharge);
            SubscribeLocalEvent<BatteryComponent, GetChargeEvent>(OnGetCharge);

            SubscribeLocalEvent<NetworkBatteryPreSync>(PreSync);
            SubscribeLocalEvent<NetworkBatteryPostSync>(PostSync);
        }

        private void OnNetBatteryRejuvenate(EntityUid uid, PowerNetworkBatteryComponent component, RejuvenateEvent args)
        {
            component.NetworkBattery.CurrentStorage = component.NetworkBattery.Capacity;
        }

        private void OnBatteryRejuvenate(EntityUid uid, BatteryComponent component, RejuvenateEvent args)
        {
            SetCharge(uid, component.MaxCharge, component);
        }

        private void OnExamine(EntityUid uid, ExaminableBatteryComponent component, ExaminedEvent args)
        {
            if (!TryComp<BatteryComponent>(uid, out var batteryComponent))
                return;
            if (args.IsInDetailsRange)
            {
                var effectiveMax = batteryComponent.MaxCharge;
                if (effectiveMax == 0)
                    effectiveMax = 1;
                var chargeFraction = batteryComponent.CurrentCharge / effectiveMax;
                var chargePercentRounded = (int)(chargeFraction * 100);
                args.PushMarkup(
                    Loc.GetString(
                        "examinable-battery-component-examine-detail",
                        ("percent", chargePercentRounded),
                        ("markupPercentColor", "green")
                    )
                );
            }
        }

        private void PreSync(NetworkBatteryPreSync ev)
        {
            // Ignoring entity pausing. If the entity was paused, neither component's data should have been changed.
            var enumerator = AllEntityQuery<PowerNetworkBatteryComponent, BatteryComponent>();
            while (enumerator.MoveNext(out var netBat, out var bat))
            {
                DebugTools.Assert(bat.CurrentCharge <= bat.MaxCharge && bat.CurrentCharge >= 0);
                netBat.NetworkBattery.Capacity = bat.MaxCharge;
                netBat.NetworkBattery.CurrentStorage = bat.CurrentCharge;
            }
        }

        private void PostSync(NetworkBatteryPostSync ev)
        {
            // Ignoring entity pausing. If the entity was paused, neither component's data should have been changed.
            var enumerator = AllEntityQuery<PowerNetworkBatteryComponent, BatteryComponent>();
            while (enumerator.MoveNext(out var uid, out var netBat, out var bat))
            {
                SetCharge(uid, netBat.NetworkBattery.CurrentStorage, bat);
            }
        }

        public override void Update(float frameTime)
        {
            // Mono
            _updateAccumulator += frameTime;
            if (_updateAccumulator < _updateInterval)
                return;
            _updateAccumulator -= _updateAccumulator;

            var query = EntityQueryEnumerator<BatterySelfRechargerComponent, BatteryComponent>();
            while (query.MoveNext(out var uid, out var comp, out var batt))
            {
                if (!comp.AutoRecharge || IsFull(uid, batt))
                    continue;

                if (comp.AutoRechargePause)
                {
                    if (comp.NextAutoRecharge > Timing.CurTime)
                        continue;
                }

                TrySetCharge(uid, batt.CurrentCharge + comp.AutoRechargeRate * _updateInterval, batt); // Frontier: Upstream - #28984
            }
        }

        /// <summary>
        /// Gets the price for the power contained in an entity's battery.
        /// </summary>
        private void CalculateBatteryPrice(EntityUid uid, BatteryComponent component, ref PriceCalculationEvent args)
        {
            args.Price += component.CurrentCharge * component.PricePerJoule;
        }

        private void OnChangeCharge(Entity<BatteryComponent> entity, ref ChangeChargeEvent args)
        {
            if (!TryComp<ChargingComponent>(entity, out var charging))
                return;

            var ev = new ChargerUpdateStatusEvent();
            RaiseLocalEvent(charging.ChargerUid, ref ev);
        }

        private void OnGetCharge(Entity<BatteryComponent> ent, ref GetChargeEvent args)
        {
            args.CurrentCharge += ent.Comp.CurrentCharge;
            args.MaxCharge += ent.Comp.MaxCharge;
        }

        public override float UseCharge(EntityUid uid, float value, BatteryComponent? battery = null)
        {
            if (value <= 0 || !Resolve(uid, ref battery) || battery.CurrentCharge == 0)
                return 0;

            var newValue = Math.Clamp(0, battery.CurrentCharge - value, battery.MaxCharge);
            var delta = newValue - battery.CurrentCharge;
            battery.CurrentCharge = newValue;

            // Apply a cooldown to the entity's self recharge if needed.
            TrySetChargeCooldown(uid);

            var ev = new ChargeChangedEvent(battery.CurrentCharge, battery.MaxCharge);
            RaiseLocalEvent(uid, ref ev);
            return delta;
        }

        public override void SetMaxCharge(EntityUid uid, float value, BatteryComponent? battery = null)
        {
            if (!Resolve(uid, ref battery))
                return;

            var old = battery.MaxCharge;
            battery.MaxCharge = Math.Max(value, 0);
            battery.CurrentCharge = Math.Min(battery.CurrentCharge, battery.MaxCharge);
            if (MathHelper.CloseTo(battery.MaxCharge, old))
                return;

            var ev = new ChargeChangedEvent(battery.CurrentCharge, battery.MaxCharge);
            RaiseLocalEvent(uid, ref ev);
        }

        public void SetCharge(EntityUid uid, float value, BatteryComponent? battery = null)
        {
            if (!Resolve(uid, ref battery))
                return;

            var old = battery.CurrentCharge;
            battery.CurrentCharge = MathHelper.Clamp(value, 0, battery.MaxCharge);
            if (MathHelper.CloseTo(battery.CurrentCharge, old) &&
                !(old != battery.CurrentCharge && battery.CurrentCharge == battery.MaxCharge))
            {
                return;
            }

            var ev = new ChargeChangedEvent(battery.CurrentCharge, battery.MaxCharge);
            RaiseLocalEvent(uid, ref ev);
        }

        /// <summary>
        /// Changes the current battery charge by some value
        /// </summary>
        public override float ChangeCharge(EntityUid uid, float value, BatteryComponent? battery = null)
        {
            if (!Resolve(uid, ref battery))
                return 0;

            var newValue = Math.Clamp(battery.CurrentCharge + value, 0, battery.MaxCharge);
            var delta = newValue - battery.CurrentCharge;
            battery.CurrentCharge = newValue;

            TrySetChargeCooldown(uid);

            var ev = new ChargeChangedEvent(battery.CurrentCharge, battery.MaxCharge);
            RaiseLocalEvent(uid, ref ev);
            return delta;
        }

        public override void TrySetChargeCooldown(EntityUid uid, float value = -1)
        {
            if (!TryComp<BatterySelfRechargerComponent>(uid, out var batteryself))
                return;

            if (!batteryself.AutoRechargePause)
                return;

            // If no answer or a negative is given for value, use the default from AutoRechargePauseTime.
            if (value < 0)
                value = batteryself.AutoRechargePauseTime;

            if (Timing.CurTime + TimeSpan.FromSeconds(value) <= batteryself.NextAutoRecharge)
                return;

            SetChargeCooldown(uid, batteryself.AutoRechargePauseTime, batteryself);
        }

        /// <summary>
        /// Puts the entity's self recharge on cooldown for the specified time.
        /// </summary>
        public void SetChargeCooldown(EntityUid uid, float value, BatterySelfRechargerComponent? batteryself = null)
        {
            if (!Resolve(uid, ref batteryself))
                return;

            if (value >= 0)
                batteryself.NextAutoRecharge = Timing.CurTime + TimeSpan.FromSeconds(value);
            else
                batteryself.NextAutoRecharge = Timing.CurTime;
        }

        /// <summary>
        ///     If sufficient charge is available on the battery, use it. Otherwise, don't.
        /// </summary>
        public override bool TryUseCharge(EntityUid uid, float value, BatteryComponent? battery = null)
        {
            if (!Resolve(uid, ref battery, false) || value > battery.CurrentCharge)
                return false;

            UseCharge(uid, value, battery);
            return true;
        }

        /// <summary>
        ///     Like SetCharge, but checks for conditions like EmpDisabled before executing
        /// </summary>
        public bool TrySetCharge(EntityUid uid, float value, BatteryComponent? battery = null) // Frontier: Upstream - #28984
        {
            if (!Resolve(uid, ref battery, false) || HasComp<EmpDisabledComponent>(uid))
                return false;

            SetCharge(uid, value, battery);
            return true;
        }

        /// <summary>
        /// Returns whether the battery is full.
        /// </summary>
        public bool IsFull(EntityUid uid, BatteryComponent? battery = null)
        {
            if (!Resolve(uid, ref battery))
                return false;

            return battery.CurrentCharge >= battery.MaxCharge;
        }

        // Goobstation
        public int GetChargeDifference(EntityUid uid, BatteryComponent? battery = null) // Debug
        {
            if (!Resolve(uid, ref battery))
                return 0;

            return Convert.ToInt32(battery.MaxCharge - battery.CurrentCharge);
        }
        public float AddCharge(EntityUid uid, float value, BatteryComponent? battery = null)
        {
            if (value <= 0 || !Resolve(uid, ref battery))
                return 0;

            var newValue = Math.Clamp(battery.CurrentCharge + value, 0, battery.MaxCharge);
            battery.CurrentCharge = newValue;
            var ev = new ChargeChangedEvent(battery.CurrentCharge, battery.MaxCharge);
            RaiseLocalEvent(uid, ref ev);
            return newValue;
        }
            // WD EDIT START
        public bool TryGetBatteryComponent(EntityUid uid, [NotNullWhen(true)] out BatteryComponent? battery,
            [NotNullWhen(true)] out EntityUid? batteryUid)
        {
            if (TryComp(uid, out battery))
            {
                batteryUid = uid;
                return true;
            }

            if (!_containers.TryGetContainer(uid, "cell_slot", out var container)
                || container is not ContainerSlot slot)
            {
                battery = null;
                batteryUid = null;
                return false;
            }

            batteryUid = slot.ContainedEntity;

            if (batteryUid != null)
                return TryComp(batteryUid, out battery);

            battery = null;
            return false;
        }
        // WD EDIT END
    }
}

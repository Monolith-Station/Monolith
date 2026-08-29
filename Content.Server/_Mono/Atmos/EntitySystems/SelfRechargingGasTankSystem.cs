using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared._Mono.Atmos.Components;

namespace Content.Server._Mono.Atmos.EntitySystems;

/// <summary>
/// Automatically regenerates the contents of gas tanks with
/// a SelfRechargingGasTankComponent.
/// </summary>
public sealed class SelfRechargingGasTankSystem : EntitySystem
{
    [Dependency] private readonly GasTankSystem _gasTankSystem = default!;

    private const float UpdateInterval = 1f;
    private float _timer;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _timer += frameTime;

        if (_timer < UpdateInterval)
            return;

        var deltaTime = _timer;
        _timer = 0f;

        var query = EntityQueryEnumerator<GasTankComponent, SelfRechargingGasTankComponent>();

        while (query.MoveNext(out var uid, out var tank, out var recharge))
        {
            var air = tank.Air;

            if (air == null)
                continue;

            if (recharge.RechargeRate <= 0f || recharge.Gases.Count == 0)
                continue;

            if (air.Pressure >= recharge.MaxPressure)
                continue;

            var totalRatio = 0f;

            foreach (var ratio in recharge.Gases.Values)
            {
                if (ratio > 0f)
                    totalRatio += ratio;
            }

            if (totalRatio <= 0f)
                continue;

            // Amount of gas to add during this update.
            var amount = recharge.RechargeRate * deltaTime;

            // P = nRT/V
            // n = PV/RT
            var pressureDifference = recharge.MaxPressure - air.Pressure;

            var maxMoles = pressureDifference * air.Volume /
                           (Atmospherics.R * recharge.GasTemperature);

            amount = MathF.Min(amount, maxMoles);

            if (amount <= 0f)
                continue;

            var gas = new GasMixture(air.Volume)
            {
                Temperature = recharge.GasTemperature
            };

            foreach (var (gasType, ratio) in recharge.Gases)
            {
                if (ratio <= 0f)
                    continue;

                var normalizedRatio = ratio / totalRatio;

                gas.AdjustMoles(
                    gasType,
                    amount * normalizedRatio);
            }

            _gasTankSystem.AssumeAir((uid, tank), gas);
        }
    }
}
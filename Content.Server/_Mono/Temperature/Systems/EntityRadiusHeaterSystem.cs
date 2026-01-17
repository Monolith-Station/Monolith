using Content.Server._Mono.Temperature.Components;
using Content.Server.Power.Components;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server._Mono.Temperature.Systems;

/// <summary>
/// Gives thermal energy to nearby entities.
/// </summary>
public sealed class EntityRadiusHeaterSystem : EntitySystem
{
    [Dependency] private readonly TemperatureSystem _temp = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private readonly float _updateCooldown = 1f;
    private TimeSpan _updateTimer = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        if (_updateTimer < TimeSpan.FromSeconds(_updateCooldown))
        {
            _updateTimer += TimeSpan.FromSeconds(frameTime);
            return;
        }

        var eqe = EntityQueryEnumerator<EntityRadiusHeaterComponent>();

        while (eqe.MoveNext(out var uid, out var comp))
        {
            if (comp.RequireActivation && TryComp<ItemToggleComponent>(uid, out var toggle))
            {
                if (!toggle.Activated)
                    continue;
            }

            if (comp.RequireActivation && TryComp<ApcPowerReceiverComponent>(uid, out var apc))
            {
                if (apc.PowerDisabled)
                    continue;
            }

            var nearby = _lookup.GetEntitiesInRange<TemperatureComponent>(Transform(uid).Coordinates, comp.Radius);
            foreach (var ent in nearby)
            {
                _temp.ChangeHeat(uid, CalculateThermalEnergy(Transform(ent), Transform(uid), comp));
            }
        }


        _updateTimer = TimeSpan.Zero;
    }

    public float CalculateThermalEnergy(TransformComponent xform,
        TransformComponent heaterXform,
        EntityRadiusHeaterComponent comp)
    {
        if (!xform.Coordinates.TryDistance(EntityManager, heaterXform.Coordinates, out var distance))
            return 0f;

        var c = distance / comp.Radius;
        if (c < 0)
            return 0;

        return c * comp.ThermalEnergy;
    }
}

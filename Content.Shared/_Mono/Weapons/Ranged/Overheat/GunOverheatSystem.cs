using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Mono.Weapons.Ranged.Overheat;

public sealed class GunOverheatSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;

    [Dependency] private EntityQuery<GunComponent> _gunQuery = default!;
    private HashSet<Entity<GunOverheatComponent, GunComponent>> _activeGuns = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<GunOverheatComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<GunOverheatComponent, GunRefreshModifiersEvent>(OnRefresh);
    }

    public override void Update(float frameTime)
    {
        foreach (var ent in _activeGuns)
        {
            ent.Comp1.Heat = Math.Clamp(ent.Comp1.Heat - ent.Comp1.HeatDissipation*frameTime, 0, ent.Comp1.HeatCapacity);

            if (ent.Comp1.Heat > 0)
                continue;

            _activeGuns.Remove(ent);
            _gun.RefreshModifiers((ent.Owner, ent.Comp2));
        }
    }

    private void OnGunShot(Entity<GunOverheatComponent> ent, ref GunShotEvent ev)
    {
        if (!_gunQuery.TryComp(ent, out var gun))
            return;

        ent.Comp.Heat = Math.Clamp(ent.Comp.Heat + ent.Comp.HeatPerShot, 0, ent.Comp.HeatCapacity);

        _activeGuns.Add((ent.Owner, ent.Comp, gun));
        _gun.RefreshModifiers((ent, gun), ev.User);
    }

    private void OnRefresh(Entity<GunOverheatComponent> ent, ref GunRefreshModifiersEvent ev)
    {
        ev.MaxAngle *= CalculatePenalty(ent.Comp.SpreadPenalty, ent.Comp);
        ev.MinAngle *= CalculatePenalty(ent.Comp.SpreadPenalty, ent.Comp);

        ev.FireRate /= CalculatePenalty(ent.Comp.FireRatePenalty, ent.Comp);
    }

    public float CalculatePenalty(float penalty, GunOverheatComponent overheat)
    {
        var i = Math.Pow(overheat.Heat / overheat.HeatCapacity, overheat.PenaltyExponent);
        var nP = penalty - (penalty-1) * (1-i);

        return (float) nP;
    }
}

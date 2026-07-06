using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Mono.Weapons.Ranged.Overheat;

public abstract class SharedGunOverheatSystem : EntitySystem
{
    private float _updateCooldown = 0.25f;
    private float _updateTimer;

    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GunOverheatComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<GunOverheatComponent, GunRefreshModifiersEvent>(OnRefresh);
    }

    public override void Update(float frameTime)
    {
        if (_updateTimer < _updateCooldown)
        {
            _updateTimer += frameTime;
            return;
        }

        var ents = EntityQueryEnumerator<GunOverheatComponent, GunComponent>();

        while (ents.MoveNext(out _, out var comp, out _))
        {
            if (comp.Heat == 0)
                continue;

            Math.Clamp(comp.Heat -= comp.HeatDissipation*_updateCooldown, 0, comp.HeatCapacity);
        }

        _updateTimer -= _updateCooldown;
    }

    private void OnGunShot(Entity<GunOverheatComponent> ent, ref GunShotEvent ev)
    {
        if (!TryComp<GunComponent>(ent, out var gun))
            return;

        ent.Comp.Heat = Math.Clamp(ent.Comp.Heat + ent.Comp.HeatPerShot, 0, ent.Comp.HeatCapacity);
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
        var i = Math.Pow(overheat.Heat / overheat.HeatCapacity, 0.25f);
        var nP = penalty - (penalty-1) * (1-i);

        return (float) nP;
    }
}

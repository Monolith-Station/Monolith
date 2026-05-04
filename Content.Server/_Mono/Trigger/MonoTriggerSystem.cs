using Content.Server.Explosion.EntitySystems;
using Content.Server.Lightning;
using Content.Shared.Projectiles;
using Robust.Shared.Random;

namespace Content.Server._Mono.Trigger;

public sealed partial class MonoTriggerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LightningOnTriggerComponent, TriggerEvent>(OnTriggerLightning);
        SubscribeLocalEvent<TriggerOnProjectileSpentComponent, ProjectileSpentEvent>(OnProjectileSpent);
    }

    private void OnTriggerLightning(Entity<LightningOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (!_random.Prob(ent.Comp.Chance))
            return;

        _lightning.ShootRandomLightnings(ent, ent.Comp.Range, ent.Comp.Count, ent.Comp.LightningProto, ent.Comp.ArcDepth, ent.Comp.LightningEffects);
    }

    private void OnProjectileSpent(Entity<TriggerOnProjectileSpentComponent> ent, ref ProjectileSpentEvent args)
    {
        _trigger.Trigger(ent);
    }
}

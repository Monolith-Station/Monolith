using Content.Server.Explosion.EntitySystems;
using Content.Server.Lightning;
using Robust.Shared.Random;

namespace Content.Server._Mono.Trigger;

public sealed partial class TriggerSystem : EntitySystem
{
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LightningOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<LightningOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (!_random.Prob(ent.Comp.Chance))
            return;

        _lightning.ShootRandomLightnings(ent, ent.Comp.Range, ent.Comp.Count, ent.Comp.LightningProto, ent.Comp.ArcDepth, ent.Comp.LightningEffects);
    }
}

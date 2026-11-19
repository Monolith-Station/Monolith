using Content.Server.Explosion.EntitySystems;

namespace Content.Server._Mono.Spawning;

public sealed partial class SpawnEntityOnTriggerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnEntityOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<SpawnEntityOnTriggerComponent> ent, ref TriggerEvent args)
    {
        SpawnAtPosition(ent.Comp.Prototype, Transform(ent).Coordinates);

        args.Handled = true;
    }
}
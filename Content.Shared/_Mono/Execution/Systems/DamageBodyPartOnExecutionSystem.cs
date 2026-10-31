using Content.Shared._Mono.Execution.Components;
using Content.Shared.Damage;

namespace Content.Shared._Mono.Execution.Systems;

public sealed partial class DamageBodyPartOnExecutionSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageBodyPartOnExecutionComponent, MeleeExecutionCompletedEvent>(OnMeleeExecutionCompleted);
    }

    private void OnMeleeExecutionCompleted(Entity<DamageBodyPartOnExecutionComponent> ent, ref MeleeExecutionCompletedEvent args)
    {
        _damageable.TryChangeDamage(args.Target, ent.Comp.Damage, canSever: ent.Comp.CanSever, targetPart: ent.Comp.Part, ignoreResistances: ent.Comp.IgnoreResistances, tool: ent);
    }
}
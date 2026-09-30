using Content.Shared._Mono.Weapons.Ranged.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Mono.Weapons.Ranged.Systems;

public sealed partial class ProjectileGridPhaseSystem : EntitySystem
{
    [Dependency] private EntityQuery<ProjectileGridPhaseComponent> _phaseQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileGridPhaseComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<ProjectileGridPhaseComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnInit(Entity<ProjectileGridPhaseComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        ent.Comp.SourceGrid = xform.GridUid;
    }

    private void OnPreventCollide(Entity<ProjectileGridPhaseComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.SourceGrid == null)
            return;

        // don't hit parent grid or projectiles with same parent grid
        if (ent.Comp.SourceGrid == Transform(args.OtherEntity).GridUid
            || _phaseQuery.TryComp(args.OtherEntity, out var otherPhase)
                && otherPhase.SourceGrid == ent.Comp.SourceGrid)
            args.Cancelled = true;
    }
}

using Content.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Mono.ArmorPiercing;

/// <summary>
/// Handles collision logic for projectiles with ArmorPiercingComponent.
/// </summary>
public sealed class ArmorPiercingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorPiercingComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<ArmorPiercingComponent> ent, ref PreventCollideEvent args)
    {
        var comp = ent.Comp;

        var isWall = (args.OtherFixture.CollisionLayer & (int)CollisionGroup.Impassable) != 0;

        if (!isWall)
            return;

        if (!TryComp(args.OtherEntity, out ArmorThicknessComponent? armorThickness))
            return;

        if (!armorThickness.CanBePierced)
            return;

        var alreadyPierced = comp.PiercedEntities.Contains(args.OtherEntity);

        if (alreadyPierced)
        {
            args.Cancelled = true;
            return;
        }

        if (comp.PiercingThickness < armorThickness.Thickness)
            return;

        args.Cancelled = true;

        comp.PiercedEntities.Add(args.OtherEntity);

        comp.PiercingThickness = Math.Max(1, comp.PiercingThickness / 2);

        Dirty(ent, comp);
    }
}

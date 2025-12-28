using Content.Shared.Weapons.Ranged.Events;
using Content.Shared._DV.Weapons.Ranged.Components;

namespace Content.Shared._DV.Weapons.Ranged.Systems;

public sealed class GunAccuracyModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAccuracyModifierComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnGunRefreshModifiers(Entity<PlayerAccuracyModifierComponent> ent, ref GunRefreshModifiersEvent args)
    {
        // MONO: Content.Shared._Mono.Weapons.Ranged.Components.SkipAccuracyModifierComponent.css
        // Skip modifications if - type: SkipPlayerAccuracyModifier is present in a weapon
        if (HasComp<SkipPlayerAccuracyModifierComponent>(args.Gun))
			return;
		
        var maxSpread = MathHelper.DegreesToRadians(ent.Comp.MaxSpreadAngle);
        args.MinAngle = Math.Clamp(args.MinAngle * ent.Comp.SpreadMultiplier, 0f, maxSpread);
        args.MaxAngle = Math.Clamp(args.MaxAngle * ent.Comp.SpreadMultiplier, 0f, maxSpread);

        args.AngleIncrease *= ent.Comp.SpreadMultiplier;
    }
}

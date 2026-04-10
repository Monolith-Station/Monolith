using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Standing;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._Mono.Company;
using Content.Shared.Damage.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Containers;

namespace Content.Shared.Damage.Systems;

public sealed class RequireProjectileTargetSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RequireProjectileTargetComponent, PreventCollideEvent>(PreventCollide);
        SubscribeLocalEvent<RequireProjectileTargetComponent, MobStateChangedEvent>(CritBulletPass);
    }

    /// <summary>
    /// Shared logic to determine if a collision should be prevented based on mob state, targeting, and company affiliation.
    /// </summary>
    /// <param name="target">The entity being hit</param>
    /// <param name="shooter">The entity doing the shooting</param>
    /// <param name="isTargeted">Whether this is a targeted shot (cursor over target when firing)</param>
    /// <returns>True if the collision should be prevented, false if it should be allowed</returns>
    public bool ShouldPreventCollision(EntityUid target, EntityUid? shooter, bool isTargeted)
    {
        // Only apply logic if the target has RequireProjectileTargetComponent and it's active
        if (!TryComp<RequireProjectileTargetComponent>(target, out var targetComp) || !targetComp.Active)
            return false;

        // // Check if shooter and target are in the same company
        // var sameCompany = false;
        // if (shooter != null &&
        //     TryComp<CompanyComponent>(shooter.Value, out var shooterCompany) &&
        //     TryComp<CompanyComponent>(target, out var targetCompany) &&
        //     !string.IsNullOrEmpty(shooterCompany.CompanyName) &&
        //     !string.IsNullOrEmpty(targetCompany.CompanyName) &&
        //     shooterCompany.CompanyName != "None" &&
        //     targetCompany.CompanyName != "None")
        // {
        //     sameCompany = shooterCompany.CompanyName == targetCompany.CompanyName;
        // }

        // Prevent hitting downed mobs ONLY if they are critical or dead
        // unless the shot is specifically targeted at them (cursor over them when firing)
        if (_mobState.IsIncapacitated(target))
        {
            // If the shot is specifically targeted at this critical/dead mob, allow the hit
            if (isTargeted)
                return false;

            // Otherwise, mob is critical or dead - prevent collision (shots pass through)
            return true;
        }

        // // If we reach here, the mob is downed but alive
        // // If shooter and target are in the same company, prevent friendly fire
        // // unless the shot is specifically targeted at them (cursor over them when firing)
        // if (sameCompany)
        // {
        //     // If the shot is specifically targeted at this same-company mob, allow the hit
        //     if (isTargeted)
        //         return false;
        //
        //     // Otherwise, prevent friendly fire
        //     return true;
        // }

        // Otherwise, allow shots to hit
        return false;
    }

    private void PreventCollide(Entity<RequireProjectileTargetComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.Active)
            return;

        var other = args.OtherEntity;

        // ONLY apply collision prevention logic to projectiles
        // This check must come FIRST to prevent non-projectiles from being affected
        if (!TryComp(other, out ProjectileComponent? projectile))
            return;

        // Check if this is a targeted projectile aimed at this specific entity
        var isTargetedAtThis = CompOrNull<TargetedProjectileComponent>(other)?.Target == ent;

        // Use shared logic to determine if collision should be prevented
        if (ShouldPreventCollision(ent, projectile.Shooter, isTargetedAtThis))
        {
            args.Cancelled = true;
            return;
        }

        // Rest of the projectile-specific logic
        if (CompOrNull<TargetedProjectileComponent>(other)?.Target != ent)
        {
            // Prevents shooting out of while inside of crates
            var shooter = projectile.Shooter;
            if (!shooter.HasValue)
                return;

            // Goobstation - Crawling
            if (TryComp<StandingStateComponent>(shooter, out var standingState) && standingState.CurrentState != StandingState.Standing)
                return;

            // ProjectileGrenades delete the entity that's shooting the projectile,
            // so it's impossible to check if the entity is in a container
            if (TerminatingOrDeleted(shooter.Value))
                return;

            // Check if target and projectile are on different maps/z-levels
            var targetXform = Transform(ent);
            var projectileXform = Transform(other);
            if (targetXform.MapID != projectileXform.MapID)
            {
                args.Cancelled = true;
                return;
            }

            if (!_container.IsEntityOrParentInContainer(shooter.Value))
                args.Cancelled = true;
        }
    }

    private void SetActive(Entity<RequireProjectileTargetComponent> ent, bool value)
    {
        if (ent.Comp.Active == value)
            return;

        ent.Comp.Active = value;
        Dirty(ent);
    }
    private void CritBulletPass(Entity<RequireProjectileTargetComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
        {
            SetActive(ent, true);
        }
        else
        {
            SetActive(ent, false);
        }
    }
}

using Content.Server.Gatherable.Components;
using Content.Shared.Destructible;
using Content.Shared.Mining.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Server.Gatherable;

// Mono - System changes.
public sealed partial class GatherableSystem
{
    private void InitializeProjectile()
    {
        SubscribeLocalEvent<GatheringProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(Entity<GatheringProjectileComponent> gathering, ref ProjectileHitEvent args)
    {
        if (!TryComp<ProjectileComponent>(gathering, out var _) ||
            gathering.Comp.Amount <= 0 || !TryComp<GatherableComponent>(args.Target, out var gatherable))
            return;

        // Frontier: gathering changes
        // bad gatherer - not strong enough
        if (_whitelistSystem.IsWhitelistFail(gatherable.ToolWhitelist, gathering.Owner))
            return;

        // Too strong (e.g. overpen) - gathers ore but destroys it
        if (TryComp<OreVeinComponent>(args.Target, out var oreVein)
            && _whitelistSystem.IsWhitelistPass(oreVein.GatherDestructionWhitelist, gathering.Owner))
            oreVein.PreventSpawning = true;
        // End Frontier: gathering changes

        // Mono
        if (gatherable.Gathered)
        {
            args.Handled = true;
            return;
        }

        Gather(args.Target, gathering, gatherable);
        gathering.Comp.Amount--;

        if (gathering.Comp.Amount <= 0)
            return;


        args.Handled = true;
    }
}

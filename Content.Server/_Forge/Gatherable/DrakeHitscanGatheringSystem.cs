using System.Numerics;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared._Forge.Gatherable;
using Content.Shared.Mobs.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Forge.Gatherable;

public sealed class DrakeHitscanGatheringSystem : EntitySystem
{
    [Dependency] private readonly GatherableSystem _gather = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const float OreTeleportGatherRadius = 1.5f;
    private const float OreTeleportSpread = 0.35f;

    /// <summary>Raycast context for the current shot (hitscan entity -> gun / shooter).</summary>
    private readonly Dictionary<EntityUid, (EntityUid Gun, EntityUid? Shooter)> _shotContext = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DrakeHitscanGatheringComponent, HitscanRaycastFiredEvent>(OnRaycastFired);
        SubscribeLocalEvent<DrakeHitscanGatheringComponent, HitscanDamageDealtEvent>(OnHitscanHit);
        SubscribeLocalEvent<DrakeHitscanGatheringComponent, ComponentShutdown>(OnHitscanShutdown); // Forge-Change
    }

    // Forge-Change-Start: miss shots never fired HitscanDamageDealt, leaving _shotContext entries.
    private void OnHitscanShutdown(Entity<DrakeHitscanGatheringComponent> ent, ref ComponentShutdown args)
    {
        _shotContext.Remove(ent);
    }
    // Forge-Change-End

    private void OnRaycastFired(Entity<DrakeHitscanGatheringComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled)
            return;

        _shotContext[ent] = (args.Gun, args.Shooter);
    }

    private void OnHitscanHit(Entity<DrakeHitscanGatheringComponent> ent, ref HitscanDamageDealtEvent ev)
    {
        if (!TryComp<GatherableComponent>(ev.Target, out var gatherable))
            return;

        if (_whitelist.IsWhitelistFail(gatherable.ToolWhitelist, ent))
            return;

        var rockCoords = _transform.GetMapCoordinates(ev.Target);

        if (!_shotContext.TryGetValue(ent, out var ctx))
            ctx = (EntityUid.Invalid, null);

        _shotContext.Remove(ent);

        _gather.Gather(ev.Target, ent, gatherable);

        if (!ent.Comp.TeleportOre)
            return;

        var collector = ResolveCollector(ent.Comp, ctx.Gun, ctx.Shooter);
        if (collector == null)
            return;

        TeleportMiningDropsToCollector(rockCoords, collector.Value, ent.Comp);
    }

    private EntityUid? ResolveCollector(DrakeHitscanGatheringComponent gathering, EntityUid gun, EntityUid? shooter)
    {
        if (gathering.CollectToGun && gun.IsValid())
            return gun;

        if (shooter != null && Exists(shooter.Value) && !HasComp<GunComponent>(shooter.Value))
            return shooter;

        if (gun.IsValid() && TryComp<TransformComponent>(gun, out var gunXform))
        {
            var parent = gunXform.ParentUid;
            if (parent.IsValid() && HasComp<MobStateComponent>(parent))
                return parent;
        }

        if (gun.IsValid())
            return gun;

        return shooter;
    }

    private void TeleportMiningDropsToCollector(MapCoordinates rockCoords, EntityUid collector, DrakeHitscanGatheringComponent gathering)
    {
        var dest = _transform.GetMapCoordinates(collector);

        if (gathering.TeleportOffset != Vector2.Zero)
            dest = dest.Offset(_transform.GetWorldRotation(collector).RotateVec(gathering.TeleportOffset));

        foreach (var drop in _lookup.GetEntitiesInRange(rockCoords, OreTeleportGatherRadius, LookupFlags.Dynamic))
        {
            if (!IsAsteroidMiningDrop(drop))
                continue;

            var offset = new Vector2(
                _random.NextFloat(-OreTeleportSpread, OreTeleportSpread),
                _random.NextFloat(-OreTeleportSpread, OreTeleportSpread));

            _transform.SetMapCoordinates(drop, dest.Offset(offset));
        }
    }

    /// <summary>
    /// Ore (steel, plasma, scrap, bluespace, etc.) and artifact fragments from asteroid veins.
    /// </summary>
    private bool IsAsteroidMiningDrop(EntityUid entity)
    {
        return _tag.HasTag(entity, "Ore") || _tag.HasTag(entity, "ArtifactFragment");
    }
}

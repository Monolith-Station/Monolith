using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Mono.StationEvents;

public sealed class AutoExtendRuleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Update(float frameTime)
    {
        var unExtendQueue = new List<EntityUid>();
        var query = EntityQueryEnumerator<AutoExtendRuleComponent, StationEventComponent>();
        while (query.MoveNext(out var ruleUid, out var extendRule, out var stationEv))
        {
            // if we're ended remove AutoExtendRuleComponent from us
            if (HasComp<EndedGameRuleComponent>(ruleUid))
            {
                unExtendQueue.Add(ruleUid);
                continue;
            }

            if (stationEv.EndTime == null)
                continue;

            // if we're too early to check then don't
            if (_timing.CurTime < stationEv.EndTime.Value - extendRule.ExtendAfterTime)
                continue;

            extendRule.UpdateAccumulator += TimeSpan.FromSeconds(frameTime);
            if (extendRule.UpdateAccumulator < extendRule.RecheckDelay)
                continue;
            extendRule.UpdateAccumulator -= extendRule.RecheckDelay;

            var hasNearbyPlayer = false;
            foreach (var entUid in extendRule.Entities)
            {
                // this is copypasted and modified from NPCSystem
                var xform = Transform(entUid);
                var ourCoords = xform.Coordinates;
                // extend our check radius if we're a grid
                var checkRadius = extendRule.PlayerCheckRadius;
                if (TryComp<MapGridComponent>(entUid, out var gridComp))
                {
                    var gridAABB = gridComp.LocalAABB;
                    checkRadius += MathF.Sqrt(gridAABB.Width*gridAABB.Width + gridAABB.Height*gridAABB.Height);
                }

                var allPlayerData = _player.GetAllPlayerData();
                foreach (var playerData in allPlayerData)
                {
                    var exists = _player.TryGetSessionById(playerData.UserId, out var session);

                    if (!exists || session == null
                        || session.AttachedEntity is not { Valid: true } playerEnt
                        || HasComp<GhostComponent>(playerEnt)
                        || _mobState.IsDead(playerEnt))
                        continue;

                    var playerCoords = Transform(playerEnt).Coordinates;

                    if (ourCoords.TryDistance(EntityManager, playerCoords, out var distance) &&
                        distance <= checkRadius)
                    {
                        hasNearbyPlayer = true;
                        break;
                    }
                }
                if (hasNearbyPlayer)
                    break;
            }

            if (hasNearbyPlayer)
                stationEv.EndTime += extendRule.ExtendBy;
        }

        foreach (var uid in unExtendQueue)
        {
            RemComp<AutoExtendRuleComponent>(uid);
        }
    }

    /// <summary>
    /// Automatically apply and configure an AutoExtendRuleComponent to specified entities.
    /// Can be used several times.
    /// </summary>
    public bool AutoExtend(Entity<StationEventComponent?> ent, List<EntityUid> entsWith, float portion = 0.125f)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (ent.Comp.Duration == null)
            return false;

        var comp = EnsureComp<AutoExtendRuleComponent>(ent);
        foreach (var entUid in entsWith)
            comp.Entities.Add(entUid);

        comp.ExtendAfterTime = ent.Comp.Duration.Value * portion;
        comp.ExtendBy = ent.Comp.Duration.Value * portion;
        comp.RecheckDelay = comp.ExtendBy * 0.5f;

        return true;
    }

    public bool AutoExtend(Entity<StationEventComponent?> ent, EntityUid entWith, float portion = 0.125f)
    {
        return AutoExtend(ent, new List<EntityUid>() { entWith }, portion);
    }
}

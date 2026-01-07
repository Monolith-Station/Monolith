using Content.Server.Ghost.Roles.Components;
using Content.Server.NPC.HTN;
using Content.Shared._Mono.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Cleanup;

/// <summary>
///     Deletes all entities with SpaceGarbageComponent.
/// </summary>
public sealed class MobCleanupSystem : BaseCleanupSystem<HTNComponent>
{
    [Dependency] private readonly CleanupHelperSystem _cleanup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private float _maxDistance;
    private float _maxGridDistance;

    private EntityQuery<GhostRoleComponent> _ghostQuery;
    private EntityQuery<CleanupImmuneComponent> _immuneQuery;

    public override void Initialize()
    {
        base.Initialize();

        _ghostQuery = GetEntityQuery<GhostRoleComponent>();
        _immuneQuery = GetEntityQuery<CleanupImmuneComponent>();

        Subs.CVar(_cfg, MonoCVars.MobCleanupDistance, val => _maxDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.CleanupMaxGridDistance, val => _maxGridDistance = val, true);
    }

    protected override bool ShouldEntityCleanup(EntityUid uid)
    {
        var xform = Transform(uid);

        return xform.GridUid == null
            && !_immuneQuery.HasComp(uid)
            && !_ghostQuery.HasComp(uid)
            && !_cleanup.HasNearbyPlayers(xform.Coordinates, _maxDistance)
            && !_cleanup.HasNearbyGrids(xform.Coordinates, _maxGridDistance);
    }
}

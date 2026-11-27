using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Robust.Shared.Containers;

namespace Content.Server._Mono.Planets;

public sealed partial class TileContactReagentSystem : EntitySystem
{
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private BloodstreamSystem _blood = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    private float _elapsed;

    public override void Update(float frameTime)
    {
        _elapsed += frameTime;
        if (_elapsed < 1f)
            return;
        _elapsed = 0f;
        var query = EntityQueryEnumerator<BloodstreamComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var blood, out var xform))
        {
            if (_containers.IsEntityInContainer(uid) || xform.GridUid == null
                || TryComp<CEZPhysicsComponent>(uid, out var z) && z.LocalPosition > 0.1f
                || !_turf.TryGetTileRef(xform.Coordinates, out var tile))
                continue;
            var definition = _turf.GetContentTileDefinition(tile.Value);
            if (definition.ContactReagent is not { } reagent || definition.ContactReagentRate <= 0f)
                continue;
            _blood.TryAddToChemicals(uid, new Solution(reagent, FixedPoint2.New(definition.ContactReagentRate)), blood);
        }
    }
}

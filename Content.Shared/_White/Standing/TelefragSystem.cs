using System.Linq;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Map;

namespace Content.Shared._White.Standing;

public sealed partial class TelefragSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedLayingDownSystem _layingDown = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public void DoTelefrag(EntityUid uid, EntityCoordinates coords, TimeSpan knockdownTime, float range = 0.4f)
    {
        if (range <= 0f)
            return;

        var entities = _lookup.GetEntitiesInRange(coords, range, LookupFlags.Dynamic);
        foreach (var ent in entities.Where(ent => ent != uid && !_standing.IsDown(ent)))
        {
            if (knockdownTime > TimeSpan.Zero && _stun.TryKnockdown(ent, knockdownTime, true))
                continue;

            _layingDown.TryLieDown(ent, behavior: DropHeldItemsBehavior.DropIfStanding);
        }
    }
}

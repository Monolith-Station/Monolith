using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Server._Mono.Gatherable;

public sealed partial class GatherableSystemHitscan : EntitySystem
{
    [Dependency] private GatherableSystem _gather = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<HitscanGatheringComponent, HitscanDamageDealtEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanGatheringComponent> ent, ref HitscanDamageDealtEvent ev)
    {
        if (!TryComp<GatherableComponent>(ev.Target, out var gatherable))
            return;

        _gather.Gather(ev.Target, ent, gatherable);
    }
}

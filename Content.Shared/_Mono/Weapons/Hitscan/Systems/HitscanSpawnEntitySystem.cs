using Content.Shared._Crescent.ShipShields;
using Content.Shared.Damage;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Network;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanSpawnEntitySystem : EntitySystem
{
    [Dependency] private SharedExplosionSystem _explosion = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanSpawnEntityComponent, HitscanRaycastFiredEvent>(OnHitscanHit, after: [ typeof(HitscanReflectSystem) ]);
    }

    private void OnHitscanHit(Entity<HitscanSpawnEntityComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled)
            return;

        if (_net.IsClient)
            return;

        foreach (var hitEntity in args.HitEntities)
        {
            if (HasComp<ShipShieldComponent>(hitEntity))
                continue; // if we don't do this, it'll spawn the entity in the centre of the ship if it hits a shield. which, with the prometheus, basically 1-taps a ton of ships who put their critical components in the centre like Mercury.
            Spawn(ent.Comp.SpawnedEntity, Transform(hitEntity).Coordinates);
        }
    }
}

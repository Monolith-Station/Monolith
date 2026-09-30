using Content.Shared._Crescent.ShipShields;
using Content.Shared.Damage;
using Content.Shared.Physics;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Mono.Fixtures;

public sealed partial class FixtureDamageDealerSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedPhysicsSystem _physic = default!;

    private float _updateCooldown = 1f;
    private float _updateTimer = 0f;

    private List<Entity<FixtureDamageDealerComponent>> _entities = [];

    public override void Initialize()
    {
        SubscribeLocalEvent<FixtureDamageDealerComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<FixtureDamageDealerComponent, EntityTerminatingEvent>(OnDispose);
    }

    private void OnInit(Entity<FixtureDamageDealerComponent> ent, ref MapInitEvent ev)
    {
        _entities.Add(ent);
    }

    private void OnDispose(Entity<FixtureDamageDealerComponent> ent, ref EntityTerminatingEvent ev)
    {
        _entities.Remove(ent);
    }

    public override void Update(float frameTime)
    {
        if (_updateTimer <= _updateCooldown)
        {
            _updateTimer += frameTime;
            return;
        }

        foreach (var ent in _entities)
        {
            var bodyEnt = ent.Owner;

            if (TryComp<ShipShieldEmitterComponent>(ent, out var shield) && shield.Shield != null)
                bodyEnt = shield.Shield.Value;

            var query = _physic.GetEntitiesIntersectingBody(bodyEnt, ent.Comp.CollisionMask);

            foreach (var queryEnt in query)
            {
                if (_whitelist.IsWhitelistFail(ent.Comp.Whitelist, queryEnt))
                    continue;

                _damageable.TryChangeDamage(queryEnt, ent.Comp.Damage);
            }
        }

        _updateTimer -= _updateCooldown;
    }

}

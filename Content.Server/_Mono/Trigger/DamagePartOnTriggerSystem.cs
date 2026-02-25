using Content.Server.Damage.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.StepTrigger;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Containers;

namespace Content.Server.Damage.Systems;

// System for damage that occurs on specific trigger, towards the user..
public sealed class DamagePartOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedContainerSystem _containerSys = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DamagePartOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(EntityUid uid, DamagePartOnTriggerComponent component, TriggerEvent args)
    {
        var xform = Transform(uid);

        if (!_containerSys.TryGetOuterContainer(uid, xform, out var container))
            return;

        args.Handled |= OnDamageTrigger(uid, container, component);
    }

    private bool OnDamageTrigger(EntityUid uid, BaseContainer container, DamagePartOnTriggerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            return false;
        }

        if (component.RequireClothingInSlot)
        {
            if (_entityManager.TryGetComponent(uid, out ClothingComponent? clothing) && clothing.InSlot != null)
                return false;
        }

        var target = container.Owner;
        var damage = new DamageSpecifier(component.Damage);


        return _damageableSystem.TryChangeDamage(target, damage, component.IgnoreResistances, origin: uid) is not null;
    }
}

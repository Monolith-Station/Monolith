using Content.Server.Destructible;
using Content.Server.Stack;
using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mono.MAWC.Scrap;

public sealed partial class ScrapCuttingSystem : EntitySystem
{
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DestructibleComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
    }

    private void OnBeforeDamage(Entity<DestructibleComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled
            || !TryComp<ScrapCuttingComponent>(args.Tool, out var cutter)
            || !TryComp<DamageableComponent>(ent, out var damageable)
            || _whitelist.IsBlacklistPass(cutter.Blacklist, ent))
            return;

        if (EntityManager.IsQueuedForDeletion(ent))
        {
            args.Cancelled = true;
            return;
        }

        var damage = args.Damage;
        if (damageable.DamageModifierSetId is { } setId && _proto.Resolve(setId, out var set))
            damage = DamageSpecifier.ApplyModifierSet(damage, set);

        if (damageable.TotalDamage + damage.GetTotal() < _destructible.DestroyedAt(ent, ent.Comp))
            return;

        args.Cancelled = true;

        var amount = GetYield(cutter, ent);
        if (amount > 0)
            _stack.SetCount(Spawn(cutter.Scrap, Transform(ent).Coordinates), amount);

        QueueDel(ent);
    }

    private int GetYield(ScrapCuttingComponent cutter, EntityUid target)
    {
        foreach (var yield in cutter.Yields)
        {
            if (_whitelist.IsWhitelistPass(yield.Whitelist, target))
                return _random.Next(yield.Min, yield.Max + 1);
        }

        return cutter.DefaultYield;
    }
}

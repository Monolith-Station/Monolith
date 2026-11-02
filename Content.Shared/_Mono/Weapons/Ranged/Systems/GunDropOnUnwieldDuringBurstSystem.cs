using System.Linq;
using Content.Shared._Mono.Weapons.Ranged.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable;
using Robust.Shared.Random;

namespace Content.Shared._Mono.Weapons.Ranged.Systems;

public sealed partial class GunDropOnUnwieldDuringBurstSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ThrowingSystem _throw = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunDropOnUnwieldDuringBurstComponent, ItemUnwieldedEvent>(OnUnwield);
    }

    private void OnUnwield(Entity<GunDropOnUnwieldDuringBurstComponent> ent, ref ItemUnwieldedEvent args)
    {
        if (!TryComp<GunComponent>(ent, out var gun) || !gun.BurstActivated)
            return;

        _hands.TryDrop(args.User, ent, checkActionBlocker: false);
        _throw.TryThrow(ent, _random.NextVector2(), user: args.User);
    }

}
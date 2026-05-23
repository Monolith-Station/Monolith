using Content.Shared._Mono.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Random;

namespace Content.Shared._Mono.Weapons.Ranged.Systems;

public sealed partial class GunRandomFirerateSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunRandomFirerateComponent, QueryFireRateMultiplierEvent>(OnQuery);
    }

    private void OnQuery(Entity<GunRandomFirerateComponent> ent, ref QueryFireRateMultiplierEvent args)
    {
        float mul = _random.NextFloat(ent.Comp.MinMul, ent.Comp.MaxMul);
        if (!ent.Comp.AsTime)
            mul = 1f / mul;

        args.ReloadTimeMul *= mul;
    }
}

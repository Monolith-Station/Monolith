using Content.Shared.Lathe;

namespace Content.Shared._DV.Lathe;

/// <summary>
/// Applies <see cref="LatheUpgradeComponent"/> modifiers when added to a lathe and removes it.
/// </summary>
public sealed class LatheUpgradeSystem : EntitySystem
{
    [Dependency] private readonly SharedLatheSystem _lathe = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheUpgradeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<LatheUpgradeComponent> ent, ref MapInitEvent args)
    {
        RemCompDeferred<LatheUpgradeComponent>(ent);

        _lathe.MultiplyLatheMultipliers(ent.Owner, ent.Comp.MaterialUseMultiplier, ent.Comp.TimeMultiplier);
    }
}

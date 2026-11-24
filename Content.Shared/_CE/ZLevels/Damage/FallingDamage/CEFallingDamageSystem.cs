/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core;
using Content.Shared.Damage;
using Content.Shared.Effects;

namespace Content.Shared._CE.ZLevels.Damage.FallingDamage;

public sealed partial class CEFallingDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEFallingDamageComponent, CEZFellOnMeEvent>(OnFallOnMe);
    }

    private void OnFallOnMe(Entity<CEFallingDamageComponent> ent, ref CEZFellOnMeEvent args)
    {
        if (_damageable.TryChangeDamage(args.Fallen, ent.Comp.Damage * args.Speed) is { Empty: false })
            _colorFlash.RaiseEffect(Color.Red, new() { args.Fallen }, CEFilter.ZPvsExcept(args.Fallen, EntityManager));
    }
}

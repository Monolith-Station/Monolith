using Content.Shared._Mono.Claws;
using Content.Shared._Mono.Claws.ClawTypes;
using Content.Shared._Mono.Claws.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;

namespace Content.Server._Mono.Claws;

/// <summary>
/// This system is supposed to update claws separately from Shared system.
/// </summary>
public sealed class ClawsSystem : SharedClawsSystem
{
    private float _updateCooldown = 1f;
    private TimeSpan _updateTimer = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClawsComponent, MeleeAttackEvent>(OnAttack);
    }

    public override void Update(float frameTime)
    {
        if (_updateTimer < TimeSpan.FromSeconds(_updateCooldown))
        {
            _updateTimer += TimeSpan.FromSeconds(frameTime);
            return;
        }

        var ents = EntityQueryEnumerator<ClawsComponent>();

        while (ents.MoveNext(out var uid, out var comp))
        {
            if (TryGetStage<Declawed>(comp, out var declawed))
                UpdateDeclaw(uid, declawed, comp, _updateCooldown);

            if (HasComp<ClawsGrowthSuppressionComponent>(uid))
            {
                comp.AccumulatedBonusGrowth = TimeSpan.Zero;
                continue;
            }

            if (!_protoMan.TryIndex(comp.ClawStage, out var claw) || !claw.CanGrow)
                continue;

            comp.GrowTimer += TimeSpan.FromSeconds(_updateCooldown) + comp.AccumulatedBonusGrowth;

            comp.AccumulatedBonusGrowth = TimeSpan.Zero;

            if (comp.GrowTimer < claw.GrowCooldown)
            {
                UpdateClaws(uid, comp); // Pretty sure we can afford that.
                Dirty(uid, comp);
                continue;
            }

            comp.GrowTimer = TimeSpan.Zero;
            comp.ClawStage = comp.Claws.GetValueOrDefault(TryGetStageNumber(comp) + 1);

            if (comp.ClawGrowthNotification != null)
                _popup.PopupEntity(Loc.GetString(comp.ClawGrowthNotification), uid, uid, PopupType.Large);

            UpdateClaws(uid, comp);
            Dirty(uid, comp);
        }

        _updateTimer -= TimeSpan.FromSeconds(_updateCooldown);

    }

    private void OnAttack(Entity<ClawsComponent> ent, ref MeleeAttackEvent args)
    {
        if (ent.Owner == args.Weapon)
            return;

        if (!TryGetStage<Declawed>(ent.Comp, out var declawed))
            return;

        if (_random.Prob(declawed.DropChanceOnMelee))
            DeclawDrop(ent, args.Weapon);
    }
}

using Content.Shared._Mono.Claws;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Mono.Claws;

/// <summary>
/// This system is supposed to update claws separately from Shared system.
/// </summary>
public sealed class ClawsSystem : SharedClawsSystem
{
    private readonly float _updateCooldown = 1f;
    private TimeSpan _updateTimer = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeclawedComponent, MeleeAttackEvent>(OnAttack);
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
            if (TryComp<DeclawedComponent>(uid, out var declawed))
                UpdateDeclaw(uid, declawed, _updateCooldown);

            if (comp.ClawStage >= comp.Stages.Count - 1 ||
                declawed != null)
                continue;

            comp.GrowTimer += TimeSpan.FromSeconds(_updateCooldown);

            if (comp.GrowTimer < comp.GrowCooldown)
            {
                UpdateClaws(uid, comp); // Pretty sure we can afford that.
                Dirty(uid, comp);
                continue;
            }

            comp.GrowTimer = TimeSpan.Zero;
            comp.ClawStage += 1;

            UpdateClaws(uid, comp);
            Dirty(uid, comp);
        }

        _updateTimer = TimeSpan.Zero;

    }

    private void OnAttack(Entity<DeclawedComponent> ent, ref MeleeAttackEvent args)
    {
        if (ent.Owner == args.Weapon)
            return;

        var r = _random.NextFloat();

        if (r < ent.Comp.DropChanceOnMelee)
            DeclawDrop(ent, args.Weapon);
    }
}

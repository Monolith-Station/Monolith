using System.Diagnostics.CodeAnalysis;
using Content.Shared._DV.Weapons.Ranged.Components;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._Mono.Claws;

/// <summary>
/// This is claw system, primarily made for lizard rework.
/// It includes stages that change melee and gun parameters in different ways.
/// </summary>
public abstract partial class SharedClawsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doafter = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClawsComponent, MeleeHitEvent>(OnAttack);
        SubscribeLocalEvent<ClawsComponent, ShotAttemptedEvent>(TryShoot);

        InitializeNailClippers();
    }
    /// <summary>
    /// Used for claw attacks - applies predicted bonus damage from stage to target.
    /// Bonus melee changes are handled in <see cref="UpdateClaws"/>
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="args"></param>
    private void OnAttack(EntityUid uid, ClawsComponent component, MeleeHitEvent args)
    {
        if (!TryGetCurrentClawStage(component, out var stage) ||
            stage.Damage == null)
            return;

        args.BonusDamage += stage.Damage;
    }

    /// <summary>
    /// Used to prevent shooting user from guns if his claws don't allow so.
    /// Gun accuracy is handled in <see cref="UpdateClaws"/>
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="args"></param>
    private void TryShoot(Entity<ClawsComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!TryGetCurrentClawStage(ent, out var stage))
            return;

        if (stage.CanShoot)
            return;

        _popup.PopupClient(Loc.GetString("clawed-shoot-fail"), Transform(ent).Coordinates, ent);
        args.Cancel();
    }

    /// <summary>
    /// Instead of capturing both melee and guns events - we will apply
    /// already existing components each stage change to our clawed entities to ensure effective ECS usage.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public void UpdateClaws(EntityUid uid, ClawsComponent component)
    {
        if (!TryGetCurrentClawStage(component, out var stage) ||
            !TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        var gunAccuracyComp = EnsureComp<PlayerAccuracyModifierComponent>(uid);
        var meleeBonusComp = EnsureComp<BonusMeleeDamageComponent>(uid);

        melee.CanWideSwing = stage.CanWideSwing;
        melee.AltDisarm = !stage.CanWideSwing;
        gunAccuracyComp.SpreadMultiplier = stage.GunSpreadMultiplier;
        _melee.ModifyBonusDamage(stage.MeleeDamageModifiers, meleeBonusComp);
    }

    public bool TryGetCurrentClawStage(ClawsComponent comp, [NotNullWhen(true)] out ClawStage? stage)
    {
        stage = comp.Declawed ? null : comp.Stages.GetValueOrDefault(comp.ClawStage);

        return stage != null;
    }
}

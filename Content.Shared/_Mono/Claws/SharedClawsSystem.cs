using System.Diagnostics.CodeAnalysis;
using Content.Shared._DV.Weapons.Ranged.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Random;

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
    [Dependency] protected readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ThrowingSystem _throw = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly MobStateSystem _state = default!;
    [Dependency] private readonly StatusEffectsSystem _effects = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClawsComponent, MeleeHitEvent>(OnAttack);
        SubscribeLocalEvent<ClawsComponent, ShotAttemptedEvent>(TryShoot);
        SubscribeLocalEvent<ClawsComponent, ExaminedEvent>(OnExamine);

        InitializeNailClippers();
    }
    /// <summary>
    /// Bonus melee changes are handled in <see cref="UpdateClaws"/>
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="args"></param>
    private void OnAttack(EntityUid uid, ClawsComponent component, MeleeHitEvent args)
    {
        if (!TryGetCurrentClawStage(component, uid, out var stage) ||
            stage.Damage == null)
        {
            if (!TryComp<DeclawedComponent>(uid, out var declawed) ||
                declawed.RawMeleeDamage == null)
                return;

            args.BonusDamage += declawed.RawMeleeDamage;
            return;
        }

        args.BonusDamage += stage.Damage;
    }

    /// <summary>
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

    private void OnExamine(EntityUid uid, ClawsComponent component, ExaminedEvent args)
    {
        args.AddMarkup(Loc.GetString(component.ClawsExaminationString + "-" + component.ClawStage));
    }

    /// <summary>
    /// Instead of capturing both melee and guns events - we will apply
    /// already existing components each stage change to our clawed entities.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public void UpdateClaws(EntityUid uid, ClawsComponent component)
    {
        if (!TryGetCurrentClawStage(component, uid, out var stage) ||
            !TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        var gunAccuracyComp = EnsureComp<PlayerAccuracyModifierComponent>(uid);
        var meleeBonusComp = EnsureComp<BonusMeleeDamageComponent>(uid);

        melee.CanWideSwing = stage.CanWideSwing;
        melee.AltDisarm = !stage.CanWideSwing;
        gunAccuracyComp.SpreadMultiplier = stage.GunSpreadMultiplier;
        _melee.ModifyBonusDamage(stage.MeleeDamageModifiers, uid, meleeBonusComp);
    }

    public bool TryGetCurrentClawStage(ClawsComponent comp, EntityUid uid, [NotNullWhen(true)] out ClawStage? stage)
    {
        stage = HasComp<DeclawedComponent>(uid)? null : comp.Stages.GetValueOrDefault(comp.ClawStage);

        return stage != null;
    }

    public bool TryGetCurrentClawStage(Entity<ClawsComponent> ent, [NotNullWhen(true)] out ClawStage? stage)
    {
        stage = HasComp<DeclawedComponent>(ent)? null : ent.Comp.Stages.GetValueOrDefault(ent.Comp.ClawStage);

        return stage != null;
    }
}

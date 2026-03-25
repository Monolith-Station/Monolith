using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._DV.Weapons.Ranged.Components;
using Content.Shared._Mono.Claws.ClawTypes;
using Content.Shared._Mono.Claws.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Mono.Claws;

/// <summary>
/// This is claw system, primarily made for lizard rework.
/// It includes stages that change melee and gun parameters in different ways.
/// </summary>
public abstract partial class SharedClawsSystem : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doafter = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] protected readonly IRobustRandom _random = default!;
    [Dependency] protected readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ThrowingSystem _throw = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly MobStateSystem _state = default!;
    [Dependency] private readonly StatusEffectsSystem _effects = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClawsComponent, GetMeleeDamageEvent>(OnMeleeAttack);
        SubscribeLocalEvent<ClawsComponent, ShotAttemptedEvent>(TryShoot);
        SubscribeLocalEvent<ClawsComponent, ExaminedEvent>(OnExamine);

        InitializeNailClippers();
    }

    private void OnMeleeAttack(Entity<ClawsComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (!TryGetStage<SharpClaw>(ent, out var stage) ||
            stage.Damage == null)
            return;

        if (args.User == args.Weapon)
            args.Damage += stage.Damage;
        else
            args.Modifiers.Add(stage.MeleeDamageModifiers);
    }

    /// <summary>
    /// Gun accuracy is handled in <see cref="UpdateClaws"/>
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="args"></param>
    private void TryShoot(Entity<ClawsComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!TryGetStage<SharpClaw>(ent.Comp, out var stage))
            return;

        if (stage.CanShoot || _tagSystem.HasTag(args.Used, "IgnoreClawPenalty"))
            return;

        _popup.PopupClient(Loc.GetString("clawed-shoot-fail"), Transform(ent).Coordinates, ent);
        args.Cancel();
    }

    private void OnExamine(Entity<ClawsComponent> ent,ref ExaminedEvent args)
    {
        if (!TryGetStage(ent, out var stage))
            return;

        args.AddMarkup(Loc.GetString(stage.ClawsExaminationString));
    }

    /// <summary>
    /// Instead of capturing both melee and guns events - we will apply
    /// already existing components each stage change to our clawed entities.
    /// </summary>
    public void UpdateClaws(EntityUid uid, ClawsComponent component)
    {
        if (!TryGetStage<SharpClaw>(component, out var stage) ||
            !TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        var gunAccuracyComp = EnsureComp<PlayerAccuracyModifierComponent>(uid);

        melee.CanWideSwing = stage.CanWideSwing;
        melee.AltDisarm = !stage.CanWideSwing;
        gunAccuracyComp.SpreadMultiplier = stage.GunSpreadMultiplier;
    }

    public void GrowClaws(TimeSpan bonusGrowth, ClawsComponent component)
    {
        component.AccumulatedBonusGrowth += bonusGrowth;
    }

    protected bool TryGetStage<T>(ClawsComponent comp, [NotNullWhen(true)] out T? stage) where T : ClawType
    {
        if (!_protoMan.TryIndex(comp.ClawStage, out var clawProto) ||
            clawProto.ClawType.GetType().Name !=  typeof(T).Name)
        {
            stage = null;
            return false;
        }

        stage = (T)clawProto.ClawType;
        return true;
    }

    protected bool TryGetStage(ClawsComponent comp, [NotNullWhen(true)] out ClawType? stage)
    {
        if (!_protoMan.TryIndex(comp.ClawStage, out var clawProto))
        {
            stage = null;
            return false;
        }

        stage = clawProto.ClawType;
        return true;
    }

    protected int TryGetStageNumber(ClawsComponent comp)
    {
        return comp.Claws.FirstOrDefault(c => c.Value == comp.ClawStage).Key;
    }
}

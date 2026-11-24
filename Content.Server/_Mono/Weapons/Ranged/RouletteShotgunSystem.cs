using Content.Shared._Mono.Weapons.Ranged.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Weapons.Ranged;

public sealed partial class RouletteShotgunSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> KnifeTag = "Knife";

    public override void Initialize()
    {
        SubscribeLocalEvent<RouletteShotgunComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<RouletteShotgunComponent, AmmoShotEvent>(OnShot);
        SubscribeLocalEvent<RouletteShotgunComponent, GunDamageModifierEvent>(OnDamageModifier);
        SubscribeLocalEvent<RouletteShotgunComponent, ExaminedEvent>(OnExamine);
    }

    private void OnInteractUsing(Entity<RouletteShotgunComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tags.HasTag(args.Used, KnifeTag))
            return;

        if (ent.Comp.BarrelRemoved)
        {
            _popup.PopupEntity(Loc.GetString("roulette-shotgun-barrel-already-removed"), ent, args.User);
            args.Handled = true;
            return;
        }

        ent.Comp.BarrelRemoved = true;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("roulette-shotgun-remove-barrel"), ent, args.User);
        _popup.PopupEntity(
            Loc.GetString("roulette-shotgun-remove-barrel-others", ("user", args.User), ("gun", ent.Owner)),
            ent,
            Filter.PvsExcept(args.User),
            true);
        args.Handled = true;
    }

    private void OnShot(Entity<RouletteShotgunComponent> ent, ref AmmoShotEvent args)
    {
        RestoreBarrel(ent);
    }

    private void OnDamageModifier(Entity<RouletteShotgunComponent> ent, ref GunDamageModifierEvent args)
    {
        if (ent.Comp.BarrelRemoved)
            args.Modifier *= ent.Comp.BarrelDamageMultiplier;
    }

    private void OnExamine(Entity<RouletteShotgunComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.BarrelRemoved)
            args.PushMarkup(Loc.GetString("roulette-shotgun-barrel-removed"));
    }

    private void RestoreBarrel(Entity<RouletteShotgunComponent> ent)
    {
        if (!ent.Comp.BarrelRemoved)
            return;

        ent.Comp.BarrelRemoved = false;
        Dirty(ent);
    }
}
